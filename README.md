# Mjölnir — реворк Ледомора (Frostner) в возвращающийся молот Тора

Этот мод **изменяет сам ванильный Ледомор (Frostner)** — тот самый молот из Горного биома
(внутреннее имя `MaceSilver`) — и превращает его в Мьёльнир.

Бросаешь Ледомор вторичной атакой — он бьёт молниями и остаётся лежать там, где упал.
Наводишь на него курсор и снова жмёшь вторичную атаку — раскат грома, и молот
возвращается в руку: один переворот при взлёте, стабильный полёт к владельцу,
на подлёте он разворачивается **рукоятью к руке**, а персонаж тянет руку, чтобы поймать.

Никаких новых предметов: твой существующий или скрафченный Ледомор — уже Мьёльнир.

## Возможности

- Бросок Ледомора с молниевым уроном и эффектами (механика копья `SpearSplitner_Lightning`)
- Молот ждёт зова: прицелься на лежащий молот и нажми вторичную атаку пустой рукой — он вернётся
- Плавный возврат: переворот → полёт к владельцу → рукоять в руку → **автоэкипировка**
- Искры молний на обычных ударах, гром и искры при возврате
- Если целишься мимо молота — обычный удар ногой (ничего не ломается)
- Команды: `mjolnir give | restyle <префаб> [масштаб]` — модель можно менять прямо в игре

## Урон

Дробящий 60 + лёд 40 + дух 20 + **молния 30**. Одноручное оружие (навык «Дубины»).

## Крафт

Рецепт Ледомора не меняется: кузница 3-го уровня — Древняя кора ×10, Серебро ×30,
Плоть Имира ×5, Морозная железа ×5.

## Установка

1. Установите **BepInExPack_Valheim** и **Jotunn** (зависимости в манифесте Thunderstore).
2. Поместите плагин в `BepInEx/plugins/Mjolnir/Mjolnir.dll`.
3. Linux: запуск через `start_game_bepinex.sh`, либо launch option Steam: `./start_game_bepinex.sh %command%`
4. Консоль F5 требует `-console` в параметрах запуска.

## Команды консоли (F5)

- `mjolnir give` — выдать Ледомор (уже реворкнутый) в инвентарь
- `mjolnir restyle <префаб> [масштаб]` — сменить модель молота, например: `mjolnir restyle SledgeDemolisher 0.6`

## Совместимость

- Valheim **1.0** (сборка l-1.0.16, Unity 6). Проверено в одиночной игре (Linux/Steam Deck).
- В мультиплеере рекомендуется установить мод на сервер и всем игрокам (иначе другие
  игроки видят обычный Ледомор, а твои броски/возвраты — только у тебя).

## Кредиты

- Создано с помощью AI-агента (OpenCode + DeepSeek) и набора инструментов [universal-modder](https://github.com/rehan-remade/universal-modder).
- Основано на: BepInExPack_Valheim (Azumatt/Vapok/Margmas), Jotunn (ValheimModding), HarmonyX.
- Используются внутриигровые ассеты Valheim (модель Ледомора, молниевые эффекты и звуки) — «bring your own game files», без перераспределения файлов игры.
- Лицензия: MIT.

---

## Mjölnir — Frostner rework (EN)

This mod reworks the vanilla Frostner mace (internal prefab `MaceSilver`) into Mjölnir, the
returning thunder hammer. Throw it with the secondary attack — it strikes with lightning and
stays where it lands. Aim at it and press secondary attack again: thunder, one flip, a smooth
flight back, and it turns handle-first into your hand with a reach-and-grab animation and
auto-equip. No new items: your existing or crafted Frostner is Mjölnir now.

Damage: Blunt 60 + Frost 40 + Spirit 20 + Lightning 30 (one-handed, Clubs skill).
The Frostner crafting recipe is unchanged.
