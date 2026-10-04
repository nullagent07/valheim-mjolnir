# Mjölnir — возвращающийся молот Тора для Valheim

Классический **Ледомор (Frostner)** получил силу грозы: теперь это **Мьёльнир**.

Бросаешь его вторичной атакой — он бьёт молниями и остаётся лежать там, где упал.
Наводишь на него курсор и снова жмёшь вторичную атаку — раскат грома, и молот
возвращается в руку: один переворот при взлёте, стабильный полёт к владельцу,
на подлёте он разворачивается **рукоятью к руке**, а персонаж тянет руку, чтобы поймать.

## Возможности

- Бросок с молниевым уроном и эффектами (снаряд использует модель Ледомора + молнии копья Splitner)
- Молот ждёт зова: прицелься на лежащий молот и нажми вторичную атаку пустой рукой — он вернётся
- Плавный возврат: переворот → полёт к владельцу → рукоять в руку → **автоэкипировка**
- Искры молний на обычных ударах, гром и искры при возврате
- Если целишься мимо молота — обычный удар ногой (ничего не ломается)
- Команды: `mjolnir give | clean | restyle <префаб> [масштаб]` — модель можно менять прямо в игре

## Урон

Дробящий 60 + лёд 40 + дух 20 + молния 30. Одноручное оружие (навык «Дубины»).

## Крафт

Кузница 3-го уровня: **Древняя кора ×10, Серебро ×30, Плоть Имира ×5, Морозная железа ×5**
(рецепт Ледомора; улучшение — серебро, 15 за уровень).

## Установка

1. Установите **BepInExPack_Valheim** и **Jotunn** (зависимости в манифесте Thunderstore).
2. Поместите плагин в `BepInEx/plugins/Mjolnir/Mjolnir.dll`.
3. Linux: запуск через `start_game_bepinex.sh`, либо launch option Steam: `./start_game_bepinex.sh %command%`
4. Консоль F5 требует `-console` в параметрах запуска.

## Команды консоли (F5)

- `mjolnir give` — выдать молот в инвентарь
- `mjolnir clean` — убрать валяющиеся дропы молота из мира
- `mjolnir restyle <префаб> [масштаб]` — сменить модель молота, например: `mjolnir restyle SledgeDemolisher 0.6`

## Совместимость

- Valheim **1.0** (сборка l-1.0.16, Unity 6). Проверено в одиночной игре (Linux/Steam Deck).
- В мультиплеере рекомендуется установить мод на сервер и всем игрокам.

## Кредиты

- Создано с помощью AI-агента (OpenCode + DeepSeek) и набора инструментов [universal-modder](https://github.com/rehan-remade/universal-modder).
- Основано на: BepInExPack_Valheim (Azumatt/Vapok/Margmas), Jotunn (ValheimModding), HarmonyX.
- Используются внутриигровые ассеты Valheim (модель Ледомора/MaceSilver, молниевые эффекты и звуки) — «bring your own game files», без перераспределения файлов игры.
- Лицензия: MIT.

---

## Mjölnir — the returning thunder hammer (EN)

The classic Frostner mace now wields the storm. Throw it with the secondary attack — it strikes
with lightning and stays where it lands. Aim at it and press secondary attack again: thunder, one
flip, a smooth flight back, and it turns handle-first into your hand with a reach-and-grab
animation and auto-equip. Frostner's model and crafting recipe, Splitner's lightning effects.

Damage: Blunt 60 + Frost 40 + Spirit 20 + Lightning 30 (one-handed, Clubs skill).
Crafting: Forge level 3 — Ancient bark ×10, Silver ×30, Ymir flesh ×5, Freeze gland ×5.
