# Заметки об API и проверке (Valheim 1.0.x)

Этот документ выполняет требование ТЗ 24.8: «проверить актуальные API и совместимость библиотек с версией игры до начала разработки». Здесь честно разделено, что подтверждено источниками, а что взято по памяти и чем защищено.

Дата проверки: 3 октября 2026. Компилятора и игры в среде разработки не было, поэтому **ничего из перечисленного не проверялось запуском**.

## 1. Подтверждено открытыми источниками

| Факт | Источник |
|---|---|
| Valheim 1.0 вышла 9 сентября 2026; добавлены биом Deep North и новый босс Kall Fimbulbringer | новости о релизе (KeenGamer, GameWave, PC Games Hardware и др.) |
| Игра перешла на Unity 6; сборки игры нацелены на `netstandard2.1`; свежая проверенная другими авторами сборка — 1.0.12 | открытый проект valheim-warband (заметки по API, сверка с декомпилированным кодом) |
| BepInExPack_Valheim 5.4.2350 вышла вместе с 1.0 (9.09.2026); на Thunderstore актуальна 5.4.2351. Для модов нужен BepInEx 5, не 6 | Thunderstore, README valheim-warband, страница мода LetsGo |
| Jötunn 2.30.x выпущен под 1.0 (на Thunderstore — 2.30.2, категория Deep North Update) | Thunderstore |
| Jötunn 2.28 не работал на 1.0: в игре изменился конструктор `Terminal.ConsoleCommand` (добавлены `remoteCommand`, `onlyAdmin`) | заметки valheim-warband |
| Включённый кроссплей отключает BepInEx (и все моды) | README valheim-warband |
| PlayerBot Wanderers (тот же жанр) строит NPC на ванильных префабах `Dverger`, `DvergerArbalest` и магах Двергров | страница мода на Nexus |
| Пользовательские модели NPC требуют бандлов ассетов; без редактора Unity остаётся ванильный клон | README Kuku's Villager Mod |

Из заметок valheim-warband (авторы сверяли с декомпилированным кодом 1.0.12) использовано как **вторичная** проверка:

- точка регистрации префабов — постфикс `ZNetScene.Awake`; `m_prefabs` — публичный список, `m_namedPrefabs` — приватный словарь, заполняемый в `Awake`;
- у диких существ нет `Tameable`; добавленный через `AddComponent`, он даёт «следовать / ждать» и сохранение команды между сессиями; `Tameable.Command(Humanoid user, bool message)`, Shift+E — переименование;
- `MonsterAI`: `SetFollowTarget`, `GetFollowTarget`, `MakeTame`;
- у `Character` публичные `Action m_onDeath` и поле фракции `m_faction`;
- `Humanoid` при каждом появлении заново выдаёт стандартную и случайную экипировку (не сохраняется) — поэтому мод очищает эти массивы и сам хранит снаряжение в ZDO;
- кнопка Shift при взаимодействии — `ZInput.GetButton("AltPlace")`;
- сигнатура `Terminal.ConsoleCommand` с именованными необязательными аргументами.

## 2. Использовано по памяти, но защищено

Эти члены игры мод вызывает без подтверждения источниками. Для каждого указано, чем ограничен риск.

### Через рефлексию (нет в исходниках как ссылка → не ломает сборку; ошибка = строка в логе)

| Член | Где | При ошибке |
|---|---|---|
| `MonsterAI.m_enableHuntPlayer`, `m_attackPlayerObjects`, `m_fleeIfLowHealth`, `m_alertRange`, `m_targetCreature`, `m_tamable` | `SurvivorPrefab`, `SurvivorBrain` | настройка пропускается (при `LogLevel 2` — запись) |
| `Tameable.m_commandable`, `m_fedDuration`, `m_monsterAI` | `SurvivorPrefab` | «следовать» может не включаться по E — тогда ищите в логе |
| `ZNetView.m_persistent`, `CharacterDrop.m_drops` | `SurvivorPrefab` | NPC может не сохраняться / оставлять ванильный дроп |
| `Humanoid.m_defaultItems`, `m_random*` | `SurvivorPrefab` | у NPC может появиться стандартная экипировка тела вдобавок к нашей |
| `Attack.m_attackAnimation`, `m_drawAnimationState` | `LoadoutService` | оружие не фильтруется по анимациям тела |
| `Inventory.m_width`, `m_height` | `SurvivorBrain` | размер инвентаря не увеличивается |
| `ZoneSystem.m_waterLevel` | `GroundUtil` | используется 30 (уровень моря) |
| `Localization.GetSelectedLanguage`, `Version.GetVersionString` | `Loc`, `Plugin` | английский язык / «unknown» в логе |
| типы `NpcTalk`, `Aggravatable` (по имени) | `SurvivorPrefab` | компоненты не удаляются |

### Напрямую в коде (при несовпадении — ошибка компиляции, правится точечно)

- `ItemDrop.ItemData`: `Clone()`, `m_dropPrefab`, `m_stack`, `m_equipped`, `m_shared.m_maxStackSize`, `m_shared.m_itemType`, `IsEquipable()`; `ItemDrop.DropItem(item, amount, position, rotation)` — всё в `Npc/ItemApi.cs`, `SurvivorBrain.cs`;
- `ObjectDB.GetItemPrefab(string)`;
- `Inventory`: `AddItem(ItemData)`, `RemoveItem(ItemData)`, `RemoveItem(ItemData, int)`, `GetAllItems()`, `Load(ZPackage)`, `Save(ZPackage)`;
- `Humanoid`: `GetInventory()`, `EquipItem(item, bool)`, `UnequipItem(item, bool)`, `IsItemEquiped(item)`;
- `Character`: `GetHealth()`, `GetMaxHealth()`, `Heal(float, bool)`, `IsDead()`, `IsTamed()`, `GetCenterPoint()`, `Damage(HitData)`, `Message(MessageHud.MessageType, string)`;
- `ZNetView`/`ZDO`/`ZPackage`: `GetZDO()`, `IsValid()`, `IsOwner()`, `Destroy()`, `Set/GetString/GetInt(int, …)`, `new ZPackage(string)`, `GetBase64()`; расширение `string.GetStableHashCode()`;
- `Localization.AddWord`, `Localize`; `ZNet.GetTimeSeconds()`;
- `Terminal.ConsoleEventArgs` (`Args`, `Context`), `Terminal.AddString`;
- `HitData.m_damage.m_damage`, `HitData.m_point`.

### Патчи Harmony (изолированы: сбой одного не отключает остальные)

| Патч | Цель | Если цели нет |
|---|---|---|
| `Patch_ZNetScene_Awake` | `ZNetScene.Awake` | **критично**: префаб NPC не зарегистрируется — виден по отсутствию строки `Survivor prefab … registered` |
| `Patch_Terminal_InitTerminal` | `Terminal.InitTerminal` (приватный статический) | консольные команды `ls_*` недоступны |
| `Patch_Tameable_GetHoverText` | `Tameable.GetHoverText` | в подсказке нет характера NPC |
| `Patch_Tameable_UseItem` | `Tameable.UseItem` (аргументы по индексам) | нельзя отдавать предметы жестом; работает `ls_give` |

## 3. Допущения о поведении, которые проверяются только в игре

1. У `Dverger` есть `Humanoid`, `MonsterAI` и `VisEquipment`. Проверка: `ls_bodyinfo`.
2. Аниматор `Dverger` содержит триггеры анимаций ударов для оружия из планировщика. Мод отбирает оружие по триггерам; если подходящих нет, использует «родное» оружие тела. Проверка: `ls_bodyinfo`, бой.
3. `Humanoid.EquipItem` надевает предметы на не-игрока и показывает их через `VisEquipment`.
4. `Tameable`, добавленный к клону Двергра, даёт рабочие «следовать / ждать» и подсказку с именем.
5. Фракция `Players` для приручённого существа даёт нужные отношения: дружелюбие к игроку, бой с монстрами.
6. Клон префаба внутри неактивного контейнера не запускает `Awake`/`ZNetView` до создания экземпляра.

Любой пункт, не подтвердившийся в игре, — это конкретная правка в одном файле; для 1–2 есть быстрый путь смены тела: `ls_prefabs`, `ls_trybody <префаб>`.

## 4. Что проверить при обновлении игры

1. Версия игры пишется в журнал при старте (`Game version: …`).
2. Если пропала строка `Survivor prefab … registered` — проверьте `ZNetScene.Awake` и `m_namedPrefabs`.
3. Если нет команд `ls_*` — проверьте `Terminal.InitTerminal` и конструктор `Terminal.ConsoleCommand`.
4. `ls_checkitems` покажет имена предметов, исчезнувшие или переименованные в новой версии.
