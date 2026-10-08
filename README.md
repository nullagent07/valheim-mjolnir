# Mjölnir — реворк Ледомора (Frostner) в возвращающийся молот Тора

Этот мод **изменяет сам ванильный Ледомор (Frostner)** — тот самый молот из Горного биома
(внутреннее имя `MaceSilver`) — и превращает его в Мьёльнир.

Бросаешь Ледомор вторичной атакой — он летит **бойком вперёд**, как молот Тора, бьёт молниями
и **втыкается бойком** туда, куда попал. Молот при этом остаётся в инвентаре — пустеет только рука.
Жмёшь вторичную атаку пустой рукой (или кнопку молота на панели) — раскат грома, и молот
возвращается с **любого расстояния, без прицеливания**: ровный полёт к владельцу, на подлёте
он разворачивается **рукоятью к руке**, а персонаж тянет руку, чтобы поймать.

Никаких новых предметов: твой существующий или скрафченный Ледомор — уже Мьёльнир.

## Возможности

- Бросок Ледомора с молниевым уроном и эффектами (механика копья `SpearSplitner_Lightning`)
- Полёт бойком вперёд (без кувырков, лёгкое вращение вокруг рукояти), втыкается бойком
- Во время полёта молот остаётся в инвентаре — потерять или задюпать его нельзя
- Возврат по вторичной атаке пустой рукой или по кнопке молота на панели — с любого расстояния, без прицела
- Промах (ни во что не попал за 6 с) — молот сам летит обратно
- Плавный возврат: полёт к владельцу → рукоять в руку → **автоэкипировка**
- Искры молний на обычных ударах, гром и искры при возврате
- Пока молот в руке или не брошен — вторичная атака пустой рукой работает как обычно
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
returning thunder hammer. Throw it with the secondary attack — it flies head-first like Thor's
hammer, strikes with lightning and sticks in head-first; it stays in your inventory while out.
Press secondary attack with empty hands (or its hotbar key) to recall it from any distance, no
aiming: thunder, a straight flight back, and it turns handle-first into your hand with a
reach-and-grab animation and auto-equip. No new items: your existing or crafted Frostner is Mjölnir now.

Damage: Blunt 60 + Frost 40 + Spirit 20 + Lightning 30 (one-handed, Clubs skill).
The Frostner crafting recipe is unchanged.
