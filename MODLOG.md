# MODLOG — Gungnir (возвращающееся копьё для Valheim)

Журнал разработки. Ведётся по правилам universal-modder (`skills/mod-any-game`).
Рабочая папка: `/home/deck/valheim-mods/gungnir`. Декомпиляция (вне репо, не публиковать): `/home/deck/valheim-decomp`.

## Идея

Копьё «Гунгнир»: кидаешь — оно пробивает/бьёт цель, не падает на землю, а возвращается в руку бросившему.

## Факты окружения

- Игра: Valheim 1.0, Steam appid **892970**, buildid **25527674**, установлена нативно под Linux (Steam Deck): `/home/deck/.local/share/Steam/steamapps/common/Valheim`, бинарь `valheim.x86_64` (ELF x86-64), `UnityPlayer.so`.
- Движок: **Unity 6000.0.75f1 (Unity 6)**, backend **Mono**.
- Код игры: `valheim_Data/Managed/assembly_valheim.dll` (2.5 МБ, основной), `Assembly-CSharp.dll` (23 КБ, сэмплы/заглушки), `assembly_utils.dll` (234 КБ).
- Античит: нет. Сейвы: `~/.config/unity3d/IronGate/Valheim/` (миры, персонажи). Лог игры: там же `Player.log`.
- Загрузчиков мод не установлено на момент старта.

## Стек моддинга (актуальный под 1.0)

- **BepInExPack_Valheim 5.4.2351** (denikson, поддерживают Azumatt/Vapok/Margmas; кастомный BepInEx 5.4.23.5, обновлён под 1.0). Есть Linux-скрипт `start_game_bepinex.sh` + `libdoorstop_x64.so`. Запуск через Steam launch options: `./start_game_bepinex.sh %command%`.
- **Jotunn 2.30.2** (ValheimModding), собран под **net462** → наш плагин собираем под **net472**.
- .NET SDK 8.0.425 установлен в `~/.dotnet` (симлинк в `~/.local/bin/dotnet`). `ilspycmd 8.2` (запуск с `DOTNET_ROOT=~/.dotnet`, `DOTNET_ROLL_FORWARD=LatestMajor`).
- Стейдж пакетов: `/home/deck/valheim-mods/_stage/{bepinex,jotunn}`.

## Что выяснено из кода (assembly_valheim, декомпиляция)

### Бросок копья
- `Attack.m_attackType == AttackType.Projectile` → `ProjectileAttackTriggered()` → `FireProjectileBurst()`.
- `FireProjectileBurst()`: `Instantiate(m_attackProjectile)` → строит `HitData` из `m_weapon` → `IProjectile.Setup(m_character, velocity, hitNoise, hitData, m_weapon, m_lastUsedAmmo)`; запоминает `m_weapon.m_lastProjectile`.
- `Attack.m_consumeItem == true` → `ConsumeItem()` (Attack.cs ~656/672): `UnequipItem` + `Inventory.RemoveItem(m_weapon)` — **копьё покидает инвентарь в момент броска**.
- Поля `Attack` (публичные): `m_attackProjectile` (линия 210), `m_consumeItem` (66), `m_attackType` (54).

### Полёт снаряда (`Projectile.cs`)
- Публичные поля, важные для нас: `m_respawnItemOnHit` (105), `m_stayAfterHitStatic` (65), `m_stayAfterHitDynamic` (67), `m_attachToRigidBody` (71), `m_attachToClosestBone` (73), `m_ttl` (53), `m_spawnOnHit` (109), `m_owner`/`m_weapon` — **приватные**, но приходят в `Setup(...)`.
- `Setup(owner, velocity, hitNoise, hitData, item, ammo)`: `m_respawnItemOnHit → m_spawnItem = item`; `m_startPoint`; `m_hasLeftShields`.
- `FixedUpdate` (только у владельца ZDO): гравитация/драг, raycast по маске, TTL (`if m_ttl > 0`), `ShieldGenerator.CheckProjectile`.
- `OnHit(collider, hitPoint, water, normal)`: урон (`destructible.Damage(hitData)`), эффекты, `SpawnOnHit` → при `m_spawnItem != null` вызывает `ItemDrop.DropItem(item, 1, pos, rot)`; затем `m_didHit = true`, `m_ttl = m_stayTTL`; уничтожение снаряда: по rigidbody-ветке `if (!m_stayAfterHitDynamic) Destroy` либо `if (!m_stayAfterHitStatic) Destroy`. Есть `RPC_OnHit`, `RPC_Attach`.
- `m_onHit` — обычный делегат `OnProjectileHit(Collider, Vector3, bool)` (публичное поле): можно и без Harmony, но выбраны Harmony-патчи.

### Что нужно для возврата
- `Inventory.AddItem(ItemDrop.ItemData item)` (Inventory.cs:112) — кладёт **тот же** объект ItemData в свободный слот; есть `ContainsItem(ItemData)`.
- `ItemDrop.DropItem(ItemData item, int amount, Vector3 pos, Quaternion rot)` (ItemDrop.cs:1798) — запасной вариант (полный инвентарь).
- Рука: `VisEquipment.m_rightHand` (public Transform), `VisEquipment` — компонент на персонаже; в `Humanoid` поле `m_visEquipment` **protected**, поэтому берём `owner.GetComponent<VisEquipment>()`.
- `Character.Message(MessageHud.MessageType, string, int, Sprite, bool)` (Character.cs:3784) — всплывающие сообщения.
- `ZNetScene.instance.Destroy(gameObject)` — корректное уничтожение ZDO-объекта.

### Jotunn API (декомпиляция 2.30.2)
- `new CustomItem(name, basePrefabName, ItemConfig)` → клон ванильного префаба; `ItemManager.Instance.AddItem(...)` (проверяет IsValid; без Recipe иконка не обязательна).
- `PrefabManager.Instance.GetPrefab(name)`, `CreateClonedPrefab(name, base)`; событие `PrefabManager.OnVanillaPrefabsAvailable` — повторная попытка, если префаб ещё не доступен.
- `ItemConfig { Name, Description }` — имя/описание (можно токенами `$item_gungnir`).
- `CustomLocalization.AddTranslation(language, token, text)` + `LocalizationManager.Instance.AddLocalization(...)`.
- `CommandManager.Instance.AddConsoleCommand(ConsoleCommand)`; `ConsoleCommand { Name, Help, Run(args) }`.
- `[BepInDependency(Jotunn.Main.ModGuid)]`, `Jotunn.Main.ModGuid == "com.jotunn.jotunn"`.

### Префабы копий (строки из `StreamingAssets/SoftRef/Bundles`, 1.0 — Addressables)
Найдены: `SpearFlint`, `SpearChitin`, `SpearWolfFang`, `SpearCarapace`, `SpearSplitner`, `SpearDeepNorth`, `SpearAncientbark`(?), `SpearGold`, `SpearWood`, `SpearThrow`/`SpearThrown` (анимации/состояния?). Выбран базис с фолбэком: DeepNorth → Carapace → WolfFang → Flint.

## Маршрут

BepInEx 5 + Jotunn + HarmonyX (managed patch). Снаряд не подменяем: клон копья наследует ванильный `Attack.m_attackProjectile`, а патчи:
1. `Projectile.Setup` postfix — если `item.m_shared.m_name == "$item_gungnir"`: выключаем respawn/attach, включаем stay-флаги, `m_ttl = 0`, вешаем компонент `GungnirProjectile`.
2. `Projectile.OnHit` postfix — если компонент есть: старт возврата.
3. `GungnirProjectile` — полёт → возврат (к `m_rightHand`) → `Inventory.AddItem` (или DropItem при полном инвентаре) → уничтожение снаряда. Safety: `OnDestroy` дропает предмет на месте, чтобы копьё не терялось; защита от дублирования через `ContainsItem`.

## Оракул (проверка)

- Свой лог: `~/.config/unity3d/IronGate/Valheim/gungnir.log` (не `LogOutput.log`, т.к. объект плагина может переживать не всё).
- Команды без ввода с клавиатуры: файл `~/.config/unity3d/IronGate/Valheim/gungnir-cmd.txt` (поллинг 0.5 c, команда `give`), плюс консольная команда `gungnir give` (Jotunn).
- Проверяемые события в логе: `Gungnir item created from <base>`, `attack: ...`, `projectile setup`, `return start`, `catch: returned to inventory`.

## План

- [x] recon + RE (этот файл)
- [x] каркас плагина и патчи (v0.1.0)
- [ ] установка BepInEx+Jotunn в игру (после согласия, игра закрыта) + бэкап сейвов (Valheim был запущен — бэкап отложен)
- [ ] вертикальный срез: `give` → бросок → возврат (лог)
- [ ] полировка: рецепт крафта, иконка/модель, звук, спин при возврате
- [ ] демо-видео, публикация, field note в базу знаний

## Готчи (накапливаем)

1. Valheim 1.0: код игры в `assembly_valheim.dll`; `Assembly-CSharp.dll` почти пуст — не по нему искать логику.
2. Jotunn под net462 → плагин net472; на Linux собираем с `Microsoft.NETFramework.ReferenceAssemblies`.
3. `ilspycmd` требует `DOTNET_ROOT` и roll-forward (`DOTNET_ROLL_FORWARD=LatestMajor`) при SDK 8 против net6-тулзы.
4. Thunderstore-зипы Jotunn используют `\` в путях — распаковка только через 7z/bsdtar/python.
5. Valheim 1.0 хранит контент в `StreamingAssets/SoftRef/Bundles/<hash>` (3.8 ГБ) — имена префабов искать grep'ом по бинарю.
6. `m_consumeItem` у копья: предмет исчезает из инвентаря при броске — возврат обязан вернуть именно этот ItemData (quality/durability), либо предмет потерян.
