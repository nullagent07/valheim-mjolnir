# MODLOG — Mjolnir (возвращающийся молот с молниями для Valheim)

> Проект начинался как «Gungnir» — возвращающееся копьё (v0.1.x, проверено в игре). 2026-10-05 по просьбе игрока **пивот на Мьёльнир**: молот с молниями, который возвращается в руку.

## Идея

Мьёльнир: молот, который кидаешь (вторичная атака) — он бьёт молниями и остаётся в точке попадания. Чтобы вернуть: наведи курсор на молот и нажми вторичную атаку (пустой рукой) — он с вращением летит обратно и сразу встаёт в руку (автоэкипировка). Melee-удары искрят молниями, при попадании — ударная волна. Старые копии копья убираются командой `clean`.

## Факты окружения

- Игра: Valheim 1.0, Steam appid **892970**, buildid **25527674**, нативная Linux-сборка (Steam Deck): `/home/deck/.local/share/Steam/steamapps/common/Valheim`, бинарь `valheim.x86_64`.
- Движок: **Unity 6000.0.75f1 (Unity 6)**, backend **Mono**. Код игры — `valheim_Data/Managed/assembly_valheim.dll` (Assembly-CSharp.dll почти пуст).
- Античит: нет. Сейвы: `~/.config/unity3d/IronGate/Valheim/`. Лог игры: там же `Player.log`.
- Декомпиляция (вне репо, не публиковать): `/home/deck/valheim-decomp` (`valheim/`, `jotunn/`).

## Стек моддинга

- **BepInExPack_Valheim 5.4.2351** + Linux-скрипт `start_game_bepinex.sh` (`./start_game_bepinex.sh %command%` или напрямую).
- **Jotunn 2.30.2** (net462) → плагин собирается под **net472** (+ `Microsoft.NETFramework.ReferenceAssemblies`).
- .NET SDK 8.0.425 в `~/.dotnet`; `ilspycmd 8.2` (нужны `DOTNET_ROOT` и `DOTNET_ROLL_FORWARD=LatestMajor`).
- Стейдж: `/home/deck/valheim-mods/_stage/{bepinex,jotunn}`. Проект: `/home/deck/valheim-mods/mjolnir`.

## Что выяснено из кода (assembly_valheim)

### Механика броска (общая для копий)
- `Attack.m_attackType == AttackType.Projectile` → `FireProjectileBurst()`: `Instantiate(m_attackProjectile)` → `IProjectile.Setup(owner, velocity, hitNoise, hitData, m_weapon, ammo)`.
- У копья бросок — **вторичная атака** (`SharedData.m_secondaryAttack`), primary `m_attack` — горизонтальный удар.
- `Attack.m_consumeItem` → `ConsumeItem()`: `UnequipItem` + `Inventory.RemoveItem(m_weapon)` — предмет покидает инвентарь при броске.

### Projectile (снаряд)
- Поля: `m_respawnItemOnHit` (дроп предмета), `m_stayAfterHitStatic/Dynamic` (не уничтожаться), `m_attachToRigidBody/Bone`, `m_ttl`, `m_visual`, `m_rotateVisual`, `m_hitEffects`.
- `Setup` читает `m_respawnItemOnHit` и запоминает `m_spawnItem` **внутри тела** — патчить только Prefix'ом.
- `OnHit` → урон → `SpawnOnHit` (при `m_spawnItem` вызывает `ItemDrop.DropItem`, а он **клонирует** ItemData) → `m_ttl = m_stayTTL` → уничтожение, если stay-флаги false.
- `ItemDrop.DropItem(item, amount, pos, rot)`; `Inventory.AddItem(ItemData)` / `ContainsItem` / `GetAllItems` / `RemoveItem`.
- Рука: `VisEquipment.m_rightHand` (компонент `VisEquipment` на персонаже; `Humanoid.m_visEquipment` protected).

### Модели и молнии (v0.2)
- Точные имена — из `valheim_Data/StreamingAssets/SoftRef/manifest_extended` (текстовый!):
  - молот-база: **`SledgeDemolisher`** (есть также `SledgeIron`, `SledgeStagbreaker`, `SledgeGold`, `SledgeGold_BloodLightning`, `SledgeGold_FrostFire`);
  - молниевое копьё: **`SpearSplitner_Lightning`** (варианты `_Blood`, `_Nature`), снаряд **`projectile_splitner_lightning`**;
  - эффекты: `fx_lightningweapon_hit`, `fx_chainlightning_hit/spread(_red)`, `fx_Lightning(_red)`, `fx_lightningstaffprojectile_hit`, `fx_redlightning_launch/burst`; звуки: `sfx_mistlands_thunder`, `sfx_staffthunderblood_thunder`, `sfx_staff_lightning_*`.
- Модель оружия в руке — child **`attach`** (`ItemStand.GetAttachPrefab`), внутри может быть `attachobj`.
- Поля данных: `SharedData.m_damages` (мн.ч.!), `SharedData.m_hitEffect`, `Attack.m_hitEffect/m_hitTerrainEffect`, `Attack.Clone()`.

### Jotunn API
- `CustomItem(name, basePrefab, ItemConfig)`, `ItemManager.Instance.AddItem`, `PrefabManager.Instance.CreateClonedPrefab/AddPrefab/GetPrefab`, событие `OnVanillaPrefabsAvailable`.
- `LocalizationManager.Instance.GetLocalization()` (сам регистрирует), `CommandManager.Instance.AddConsoleCommand(ConsoleCommand)`.
- `[BepInDependency(Jotunn.Main.ModGuid)]`.

## Архитектура v0.2.0 (Mjolnir)

1. `CustomItem("Mjolnir", SledgeDemolisher)`; вторичная атака = клон броска `SpearSplitner_Lightning` (фолбэки: Splitner → Carapace → WolfFang → Flint).
2. Снаряд: клон `projectile_splitner_lightning`; в него копируется модель `attach` молота, назначается `m_visual`, старые рендеры выключаются (частицы/звуки снаряда остаются), `m_rotateVisual = 720` (вращение в полёте). Регистрируется `PrefabManager.AddPrefab("mjolnir_projectile")`.
3. Патчи: `Projectile.Setup` **Prefix** (respawn=false, spawnItem=null, stay-флаги, attach=false, ttl=0, вешаем `MjolnirProjectile`) + `Projectile.OnHit` **Postfix** (старт возврата).
4. Возврат: задержка 0.3 c → разгон 6→26 м/с к `m_rightHand`, кувырок 900°/c, поимка ≤1.6 м: `Inventory.AddItem` (или дроп под ноги при полном), сообщение, гром `sfx_mistlands_thunder` + искры `fx_lightningweapon_hit`. Safety: `OnDestroy` дропает предмет, чтобы молот не терялся; защита от дублей через `ContainsItem`.
5. Молнии: `m_damages.m_lightning = max(current, 30)`; `fx_lightningweapon_hit` добавлен в hit/hitTerrain эффекты атак.
6. Чистка: временный shim `Gungnir` (клон копья, чтобы старые дропы/предметы резолвились) + команда `clean`: уничтожает world-дропы `$item_mjolnir`/`$item_gungnir` через `ZNetScene.m_instances`, убирает старый `$item_gungnir` из инвентаря. Shim убрать после подтверждённой чистки.
7. Управление: файл-команды `mjolnir-cmd.txt` (`give`, `clean`, `ping`), консоль `mjolnir give|clean`, лог `~/.config/unity3d/IronGate/Valheim/mjolnir.log`.

## Архитектура v0.3.0 (управляемый возврат, MaceEldner)

- **Frostner в Valheim 1.0 удалён** — преемник: линейка одноручных булав `MaceEldner` (`_Blood`/`_Lightning`/`_Nature`). База: `MaceEldner` → фолбэк `MaceSilver` → `MaceIron` → `SledgeDemolisher`.
- Снаряд лежит там, где упал: `OnHit` postfix → состояние Lying (`Projectile.enabled=false`, `m_ttl=0`) — молот ждёт зова, подсказка `$msg_mjolnir_deployed`.
- Зов: patch `Humanoid.StartAttack(Character target, bool secondaryAttack)` **Prefix** — если локальный игрок, пустая рука (`GetCurrentWeapon()==null`) и `MjolnirProjectile.Current` лежит → проверка прицела (`GetAimDir` от `GetEyePoint`, угол ≤ ~25°, дистанция ≤ 100 м; в радиусе 3 м — без прицела) → `StartReturn`, гром у молота, сообщение. Prefix возвращает false (съедает нажатие).
- Возврат: задержка 0.15 c, разгон 10→34 м/с, у руки плавное замедление до 2.5 м/с (`ApproachDistance=5`), кувырок 480°/c (`LookRotation * Euler(spin)`), поимка ≤1.3 м → `Inventory.AddItem` + **`Humanoid.EquipItem(item, true)`** — молот сразу в руке.
- Волна удара: launch-эффекты броска очищены (`m_startEffect/m_triggerEffect/m_burstEffect/m_trailStartEffect = new EffectList()` — именно они давали волну в момент броска: `FireProjectileBurst` играет `m_burstEffect` на точке спавна), в hit-эффекты снаряда добавлены `demolisher_shockwave` + `fx_lightningweapon_hit` — волна после удара.
- Ориентация снаряда: модель `attach` повёрнута `Euler(-90,0,0)` — летит вдоль направления броска (не вертикально).
- Таймаут полёта (6 c, ни во что не попал) → предмет дропается на месте (`DropSafely`) — молот не теряется.
- Safety: `OnDestroy`/`DropSafely` дропают предмет; `Current` очищается при поимке/дропе/уничтожении.

## Проверка

- **v0.1.x (Gungnir):** полный цикл подтверждён в игре 2026-10-05 (setup → return start (hit) → catch, ~1.2 c). Найден баг дубликата (Setup postfix → исправлен на prefix), проверено человеком визуально.
- **v0.2.0 (Mjolnir):** сборка/деплой — ожидает теста в игре (close → relaunch → clean → give → бросок).

## План

- [x] recon + RE (копьё)
- [x] v0.1 вертикальный срез + фикс дубликата + спин + сообщение + clean
- [x] v0.2: молот-база, молниевый снаряд, молнии melee/возврат, чистка мира
- [ ] тест v0.2 в игре
- [ ] полировка: иконка/модель, звук броска, рецепт крафта
- [ ] демо-видео, публикация (Thunderstore), field note в базу знаний universal-modder
- [x] 2026-10-05: опубликовано — GitHub https://github.com/nullagent07/valheim-mjolnir (релиз v2.0.0 с пакетом), Thunderstore-пакет готов (dist/Mjolnir_ReturningThunderHammer-2.0.0.zip, publish check PASS), field note + PR: https://github.com/rehan-remade/universal-modder/pull/51

## Готчи (накоплено)

1. Valheim 1.0: логика в `assembly_valheim.dll`; `Assembly-CSharp.dll` почти пуст.
2. Jotunn net462 → плагин net472; сборка на Linux через `Microsoft.NETFramework.ReferenceAssemblies`.
3. `ilspycmd` требует `DOTNET_ROOT` и `DOTNET_ROLL_FORWARD=LatestMajor`.
4. Thunderstore-зипы Jotunn с `\` в путях — распаковка через 7z/bsdtar/python.
5. Контент 1.0 — в `StreamingAssets/SoftRef/Bundles/<hash>`; **точные имена искать в `SoftRef/manifest_extended`**, а не бинарным grep (тот даёт обрезки и склейки).
6. `m_consumeItem`: предмет исчезает из инвентаря при броске — возврат обязан вернуть именно этот ItemData.
7. Ложный «Valheim: RUNNING»: `pgrep -f valheim.x86_64` матчит собственную командную строку. Правильно: `pgrep -x valheim.x86_64`.
8. Бросок копья — `m_shared.m_secondaryAttack`; primary `m_attack` без снаряда.
9. **Дубликат дропа (v0.1.0→fix):** `Setup` читает `m_respawnItemOnHit` и пишет `m_spawnItem` внутри тела; postfix слишком поздно → ванильный `SpawnOnHit` дропает клон. Фикс: Prefix + `m_spawnItem = null`.
10. Возврат: спин `m_rotateVisual` работает только пока включён `Projectile`; при возврате компонент выключается и вращаем root вручную.
11. Поле урона — `SharedData.m_damages` (мн. ч.), легко опечататься в `m_damage`.
12. Модель оружия в руке — child `attach`; копию можно инстанцировать в снаряд и назначить `Projectile.m_visual`, сохранив эффекты снаряда.
13. **Frostner в Valheim 1.0 не существует** (нет ни в манифесте, ни в бандлах) — преемник `MaceEldner` (+варианты `_Blood/_Lightning/_Nature`).
14. «Волна при броске» — это `Attack.m_burstEffect`/`m_triggerEffect`/`m_startEffect`, играющиеся в `Attack.Start`/`FireProjectileBurst`/`OnAttackTrigger` (Attack.cs 540, 778, 929). У клонированной атаки их можно просто обнулить; ударную волну — добавить в hit-эффекты снаряда (`demolisher_shockwave`).
15. Нажатие вторичной атаки без оружия доходит до `Humanoid.StartAttack(null, true)` и сразу возвращает false (нет оружия) — идеальная точка перехвата кастомного действия; Prefix до busy-проверок позволяет зов даже на бегу.
