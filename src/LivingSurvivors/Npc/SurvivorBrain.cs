using System;
using System.Collections.Generic;
using System.Text;
using LivingSurvivors.Core;
using LivingSurvivors.Settings;
using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Главный компонент выжившего (Этап 1). Отвечает за то, чего нет у ванильного существа:
    /// личность (ТЗ 3.3), сохранение в ZDO (ТЗ 16), снаряжение и инвентарь, настройку ИИ по характеру,
    /// пассивную регенерацию, безопасное «подтягивание» к игроку (ТЗ 14) и обработку гибели.
    ///
    /// Передвижение, следование и бой выполняет ванильный MonsterAI вместе с Tameable —
    /// это проверенная годами логика; наш код лишь настраивает её и не дублирует.
    /// Вся логика, меняющая мир, выполняется только владельцем объекта (ZNetView.IsOwner()),
    /// чтобы не создавать конфликтов в мультиплеере (ТЗ 17).
    /// </summary>
    public sealed class SurvivorBrain : MonoBehaviour
    {
        internal static readonly int KeyProfile = "LS_Profile".GetStableHashCode();
        internal static readonly int KeyInventory = "LS_Inventory".GetStableHashCode();
        internal static readonly int KeyTier = "LS_Tier".GetStableHashCode();

        /// <summary>Ключ имени, которое ванильный Tameable показывает в подсказке.</summary>
        private static readonly int KeyTamedName = "TamedName".GetStableHashCode();

        private const float TickInterval = 1f;
        private const float SaveInterval = 15f;
        private const float TeleportCooldownSeconds = 20f;

        private ZNetView _nview;
        private Character _character;
        private Humanoid _body;
        private MonsterAI _ai;
        private Tameable _tameable;
        private Animator _animator;

        private bool _initialized;
        private bool _dead;
        private float _tick;
        private float _saveTimer;
        private float _teleportCooldown;
        private float _stuckTimer;
        private float _baseAlertRange;
        private Vector3 _lastPosition;
        private string _lastSavedInventory = "";
        private BehaviorTuning _tuning;

        public NpcProfile Profile { get; private set; }

        internal Humanoid Body
        {
            get { return _body; }
        }

        internal Character CharacterRef
        {
            get { return _character; }
        }

        internal Animator Animator
        {
            get { return _animator; }
        }

        internal bool IsDead
        {
            get { return _dead; }
        }

        internal string DisplayName
        {
            get { return Profile != null ? Profile.Name : name; }
        }

        internal bool IsFollowing
        {
            get { return _ai != null && _ai.GetFollowTarget() != null; }
        }

        // ------------------------------------------------------------------ жизненный цикл

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _body = GetComponent<Humanoid>();
            _ai = GetComponent<MonsterAI>();
            _tameable = GetComponent<Tameable>();
            _animator = GetComponentInChildren<Animator>();

            if (_ai != null) _baseAlertRange = Reflect.GetFieldOr<float>(_ai, "m_alertRange", 0f);
            if (_character != null) _character.m_onDeath += OnDeath;
        }

        private void Start()
        {
            if (_nview == null || !_nview.IsValid() || _character == null || _body == null)
            {
                Log.Warn("SurvivorBrain: required components are missing, brain disabled.");
                enabled = false;
                return;
            }

            try
            {
                if (_nview.IsOwner()) InitializeAsOwner();
                else LoadProfileReadOnly();
            }
            catch (Exception e)
            {
                Log.Error("SurvivorBrain initialization failed: " + e);
            }

            if (Profile != null && !SurvivorRegistry.Register(this))
            {
                // Дубликат: объект с таким стабильным ID уже существует (откат сохранения, сбой сети).
                if (_nview.IsOwner())
                {
                    Log.Warn("Duplicate survivor " + Profile.Id + " (" + Profile.Name + ") removed.");
                    _dead = true;
                    _nview.Destroy();
                }
                return;
            }

            _initialized = true;
        }

        private void OnDestroy()
        {
            SurvivorRegistry.Unregister(this);
            if (!_initialized || _dead) return;
            SaveInventory();
        }

        private void Update()
        {
            if (!_initialized || _dead) return;

            _tick += Time.deltaTime;
            if (_tick < TickInterval) return;

            float dt = _tick;
            _tick = 0f;

            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            OwnerTick(dt);
        }

        // ------------------------------------------------------------------ инициализация

        /// <summary>
        /// Гарантирует, что инвентарь вместит полный комплект (не меньше 5x3 клеток). Только увеличивает,
        /// и только если размеры полей удалось прочитать — иначе ничего не меняется.
        /// </summary>
        private void EnsureInventoryCapacity()
        {
            Inventory inventory = _body.GetInventory();
            if (Reflect.GetFieldOr<int>(inventory, "m_width", 99) < 5) Reflect.TrySetField(inventory, "m_width", 5);
            if (Reflect.GetFieldOr<int>(inventory, "m_height", 99) < 3) Reflect.TrySetField(inventory, "m_height", 3);
        }

        private void InitializeAsOwner()
        {
            EnsureInventoryCapacity();
            ZDO zdo = _nview.GetZDO();

            NpcProfile loaded;
            bool isNew = false;
            if (NpcProfileSerializer.TryDeserialize(zdo.GetString(KeyProfile, ""), out loaded))
            {
                Profile = loaded;
            }
            else
            {
                int seed = Guid.NewGuid().GetHashCode();
                Profile = NpcProfileGenerator.Generate(seed, SurvivorRegistry.TakenNames(), GameClock.Day);
                Profile.AddEvent(GameClock.Day, "born");
                zdo.Set(KeyProfile, NpcProfileSerializer.Serialize(Profile));
                isNew = true;
            }

            // Имя для подсказки ванильного Tameable; если игрок переименовал NPC (Shift+E) — не трогаем.
            if (string.IsNullOrEmpty(zdo.GetString(KeyTamedName, "")))
            {
                zdo.Set(KeyTamedName, Profile.Name);
            }

            string savedInventory = zdo.GetString(KeyInventory, "");
            if (!string.IsNullOrEmpty(savedInventory))
            {
                RestoreInventory(savedInventory);
            }
            else
            {
                int tier = zdo.GetInt(KeyTier, ModConfig.DefaultTier.Value);
                string summary = LoadoutService.GiveKit(this, tier);
                Log.Info(Profile.Name + " equipped: " + summary);
                SaveInventory();
            }

            ApplyTuning();
            _lastPosition = transform.position;

            if (isNew) Log.Info("New survivor: " + Describe());
        }

        private void LoadProfileReadOnly()
        {
            NpcProfile loaded;
            if (NpcProfileSerializer.TryDeserialize(_nview.GetZDO().GetString(KeyProfile, ""), out loaded))
            {
                Profile = loaded;
            }
        }

        /// <summary>Характер влияет на решения: порог отступления и дальность обнаружения угроз.</summary>
        private void ApplyTuning()
        {
            if (_ai == null || Profile == null) return;

            _tuning = BehaviorTuning.From(Profile);
            Reflect.TrySetField(_ai, "m_fleeIfLowHealth", _tuning.FleeHealthFraction);
            if (_baseAlertRange > 0f)
            {
                Reflect.TrySetField(_ai, "m_alertRange", _baseAlertRange * _tuning.AlertRangeMultiplier);
            }
        }

        // ------------------------------------------------------------------ инвентарь и снаряжение

        private void RestoreInventory(string base64)
        {
            try
            {
                _body.GetInventory().Load(new ZPackage(base64));
                ReEquipAll();
                _lastSavedInventory = base64;
            }
            catch (Exception e)
            {
                Log.Error("Inventory restore failed for " + DisplayName + ": " + e.Message);
            }
        }

        /// <summary>После загрузки инвентаря «надевает» заново всё, что было помечено надетым.</summary>
        private void ReEquipAll()
        {
            var items = new List<ItemDrop.ItemData>(_body.GetInventory().GetAllItems());
            foreach (ItemDrop.ItemData item in items)
            {
                if (item == null || !item.m_equipped) continue;
                item.m_equipped = false;
                _body.EquipItem(item, false);
            }
        }

        /// <summary>Записывает инвентарь в ZDO (только владелец, только если что-то изменилось).</summary>
        internal void SaveInventory()
        {
            if (_body == null || _nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            try
            {
                var package = new ZPackage();
                _body.GetInventory().Save(package);
                string base64 = package.GetBase64();
                if (base64 == _lastSavedInventory) return;

                _nview.GetZDO().Set(KeyInventory, base64);
                _lastSavedInventory = base64;
            }
            catch (Exception e)
            {
                Log.Warn("Inventory save failed for " + DisplayName + ": " + e.Message);
            }
        }

        /// <summary>Выдаёт предмет по имени префаба (консольная команда ls_give).</summary>
        internal bool GiveItem(string prefabName, int amount, bool equip, out string error)
        {
            error = null;
            ItemDrop.ItemData item = ItemApi.Create(prefabName, amount);
            if (item == null)
            {
                error = "unknown item prefab: " + prefabName;
                return false;
            }

            if (!_body.GetInventory().AddItem(item))
            {
                error = "no room in the inventory";
                return false;
            }

            if (equip && item.IsEquipable()) _body.EquipItem(item, false);
            SaveInventory();
            return true;
        }

        /// <summary>
        /// Игрок отдаёт предмет NPC: экипировка (по одной штуке) и боеприпасы (стопкой)
        /// переходят к выжившему, экипировка сразу надевается. Возвращает true, если действие
        /// обработано (в том числе отказом) и ванильную обработку нужно пропустить.
        /// </summary>
        internal bool TryReceiveItem(Humanoid giver, ItemDrop.ItemData item)
        {
            if (_dead || giver == null || item == null) return false;
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return false; // Этап 1: одиночная игра / хост

            if (!ItemApi.IsGearOrAmmo(item))
            {
                giver.Message(MessageHud.MessageType.Center, Loc.Format("msg_not_gear", DisplayName));
                return true;
            }

            ItemDrop.ItemData copy = item.Clone();
            copy.m_equipped = false;
            if (!ItemApi.IsAmmo(item)) copy.m_stack = 1;

            Inventory mine = _body.GetInventory();
            if (!mine.AddItem(copy))
            {
                giver.Message(MessageHud.MessageType.Center, Loc.Format("msg_inventory_full", DisplayName));
                return true;
            }

            // Предмет уходит от игрока; если он был надет у игрока — сначала снимаем.
            Inventory theirs = giver.GetInventory();
            if (giver.IsItemEquiped(item)) giver.UnequipItem(item, false);

            bool removed = copy.m_stack >= item.m_stack
                ? theirs.RemoveItem(item)
                : theirs.RemoveItem(item, copy.m_stack);
            if (!removed)
            {
                mine.RemoveItem(copy); // откат: не плодим предметы
                return true;
            }

            if (item.IsEquipable()) _body.EquipItem(copy, false);
            SaveInventory();

            giver.Message(MessageHud.MessageType.Center,
                Loc.Format("msg_gift_taken", DisplayName, ItemApi.DisplayName(copy)));
            return true;
        }

        /// <summary>Снимает и убирает всё снаряжение, затем выдаёт новый комплект (ls_kit).</summary>
        internal string ResetKit(int tier)
        {
            Inventory inventory = _body.GetInventory();
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            foreach (ItemDrop.ItemData item in items)
            {
                if (item == null) continue;
                _body.UnequipItem(item, false);
                inventory.RemoveItem(item);
            }

            string summary = LoadoutService.GiveKit(this, tier);
            SaveInventory();
            return summary;
        }

        // ------------------------------------------------------------------ команды

        /// <summary>Переключатель «следовать / ждать» через ванильный Tameable (с сохранением между сессиями).</summary>
        internal void SetFollow(Humanoid user, bool follow)
        {
            if (_tameable == null || _ai == null || user == null) return;
            if (IsFollowing == follow) return;
            _tameable.Command(user, false);
        }

        // ------------------------------------------------------------------ периодическая логика владельца

        private void OwnerTick(float dt)
        {
            _saveTimer += dt;
            if (_saveTimer >= SaveInterval)
            {
                _saveTimer = 0f;
                SaveInventory();
            }

            bool combat = InCombat();
            if (!combat) Regenerate(dt);
            CatchUp(dt, combat);
        }

        private bool InCombat()
        {
            if (_ai == null) return false;
            Character target = Reflect.GetFieldValue(_ai, "m_targetCreature") as Character;
            return target != null && !target.IsDead();
        }

        /// <summary>Медленное восстановление здоровья вне боя (упрощённый «отдых», ТЗ 9).</summary>
        private void Regenerate(float dt)
        {
            float percent = ModConfig.PassiveRegenPercentPer10s.Value;
            if (percent <= 0f) return;

            float max = _character.GetMaxHealth();
            if (_character.GetHealth() >= max) return;

            _character.Heal(max * (percent / 100f) * (dt / 10f), false);
        }

        /// <summary>
        /// Редкая безопасная коррекция позиции (ТЗ 14): только в режиме следования, только вне боя,
        /// только если NPC слишком далеко или застрял, с перезарядкой и проверкой поверхности.
        /// </summary>
        private void CatchUp(float dt, bool inCombat)
        {
            if (_ai == null || !ModConfig.EnableCatchUpTeleport.Value) return;

            GameObject follow = _ai.GetFollowTarget();
            if (follow == null)
            {
                _stuckTimer = 0f;
                _lastPosition = transform.position;
                return;
            }

            _teleportCooldown -= dt;

            Vector3 myPosition = transform.position;
            Vector3 targetPosition = follow.transform.position;
            float distance = Vector3.Distance(myPosition, targetPosition);
            float moved = Vector3.Distance(myPosition, _lastPosition);
            _lastPosition = myPosition;

            // «Застревание»: далеко от цели, но почти не двигаемся.
            if (distance > 12f && moved < 0.5f && !inCombat) _stuckTimer += dt;
            else _stuckTimer = 0f;

            bool tooFar = distance > ModConfig.CatchUpTeleportDistance.Value;
            bool stuck = _stuckTimer >= ModConfig.StuckTeleportSeconds.Value;
            if ((tooFar || stuck) && !inCombat && _teleportCooldown <= 0f)
            {
                if (TryTeleportNear(targetPosition))
                {
                    Log.Debug(DisplayName + " caught up with the player (" + (tooFar ? "too far" : "stuck") + ").");
                    _teleportCooldown = TeleportCooldownSeconds;
                    _stuckTimer = 0f;
                }
            }
        }

        /// <summary>Переносит NPC на землю в нескольких метрах от цели; false, если безопасной точки нет.</summary>
        internal bool TryTeleportNear(Vector3 target)
        {
            Vector3 away = transform.position - target;
            away.y = 0f;
            if (away.sqrMagnitude < 0.25f) away = Vector3.back;
            away.Normalize();

            Vector3 spot = target + away * 4f;
            Vector3 ground;
            if (!GroundUtil.TryGetGround(spot, 3f, 20f, out ground)) return false;
            if (GroundUtil.IsBelowWater(ground.y)) return false;

            Vector3 destination = ground + Vector3.up * 0.15f;
            Rigidbody rb = GetComponent<Rigidbody>();
            transform.position = destination;
            if (rb != null) rb.position = destination;
            return true;
        }

        // ------------------------------------------------------------------ гибель

        private void OnDeath()
        {
            if (_dead) return;
            _dead = true;

            try
            {
                if (_nview != null && _nview.IsValid() && _nview.IsOwner())
                {
                    if (Profile != null)
                    {
                        Profile.AddEvent(GameClock.Day, "died");
                        _nview.GetZDO().Set(KeyProfile, NpcProfileSerializer.Serialize(Profile));
                    }
                    if (ModConfig.DropInventoryOnDeath.Value) DropAllItems();
                }
                Log.Info(DisplayName + " died.");
            }
            catch (Exception e)
            {
                Log.Error("Death handling failed for " + DisplayName + ": " + e);
            }
            finally
            {
                SurvivorRegistry.Unregister(this);
            }
        }

        private void DropAllItems()
        {
            if (_body == null) return;

            Inventory inventory = _body.GetInventory();
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            Vector3 center = transform.position + Vector3.up * 0.8f;

            foreach (ItemDrop.ItemData item in items)
            {
                if (item == null) continue;

                item.m_equipped = false;
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-0.7f, 0.7f), 0f, UnityEngine.Random.Range(-0.7f, 0.7f));
                ItemApi.Drop(item, center + offset);
                inventory.RemoveItem(item);
            }

            _lastSavedInventory = "";
            _nview.GetZDO().Set(KeyInventory, "");
        }

        // ------------------------------------------------------------------ описания (подсказка и консоль)

        /// <summary>Короткая строка для подсказки: черты и стиль боя.</summary>
        internal string DescribeShort()
        {
            if (Profile == null) return "";

            var builder = new StringBuilder();
            for (int i = 0; i < Profile.Traits.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(Loc.TraitName(Profile.Traits[i]));
            }
            builder.Append(" | ").Append(Loc.StyleName(Profile.Style));
            return builder.ToString();
        }

        internal string Describe()
        {
            if (Profile == null) return name;
            return Profile.Name + " [" + Profile.Id + "] " + DescribeShort();
        }

        /// <summary>Подробное описание для консольной команды ls_info.</summary>
        internal List<string> DescribeFull()
        {
            var lines = new List<string>();
            if (Profile == null)
            {
                lines.Add(name + ": no profile");
                return lines;
            }

            lines.Add(string.Format("{0} [{1}], appeared on day {2}", Profile.Name, Profile.Id, Profile.BornDay));
            lines.Add("Traits / style: " + DescribeShort());
            lines.Add("Skills: " + TopSkills(5));
            lines.Add(string.Format("HP {0:0}/{1:0}, tamed: {2}, following: {3}",
                _character.GetHealth(), _character.GetMaxHealth(), _character.IsTamed(), IsFollowing));
            if (_tuning != null)
            {
                lines.Add(string.Format("AI tuning: flee below {0:P0} HP, alert range x{1:0.00}",
                    _tuning.FleeHealthFraction, _tuning.AlertRangeMultiplier));
            }
            lines.Add("Items: " + ItemSummary());
            foreach (HistoryEntry entry in Profile.History)
            {
                lines.Add(string.Format("  day {0}: {1}", entry.Day, Loc.EventText(entry)));
            }
            return lines;
        }

        private string TopSkills(int count)
        {
            var indexes = new List<int>();
            for (int i = 0; i < Profile.Skills.Length; i++) indexes.Add(i);
            indexes.Sort((a, b) => Profile.Skills[b].CompareTo(Profile.Skills[a]));

            var builder = new StringBuilder();
            for (int i = 0; i < count && i < indexes.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(Loc.SkillName((NpcSkill)indexes[i])).Append(' ').Append(Profile.Skills[indexes[i]]);
            }
            return builder.ToString();
        }

        private string ItemSummary()
        {
            var builder = new StringBuilder();
            foreach (ItemDrop.ItemData item in _body.GetInventory().GetAllItems())
            {
                if (item == null) continue;
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(item.m_equipped ? "*" : "").Append(ItemApi.DisplayName(item));
                if (item.m_stack > 1) builder.Append(" x").Append(item.m_stack);
            }
            return builder.Length > 0 ? builder.ToString() : "(empty)";
        }
    }
}
