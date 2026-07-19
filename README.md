# Fuel Station Refill

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.2.3-blue)](https://github.com/kabzon93region/FuelStationRefill/releases/tag/v1.2.3)
[![Download zip](https://img.shields.io/badge/download-zip-brightgreen)](https://github.com/kabzon93region/FuelStationRefill/releases/download/v1.2.3/FuelStationRefill_(singleplayer,host_client,headless_all)_v1.2.3_2026-07-19.zip)
[![EFT](https://img.shields.io/badge/EFT-16.9-orange)](https://www.escapefromtarkov.com/)
[![SPT](https://img.shields.io/badge/SPT-4.0.13-blue)](https://sp-tarkov.com/)
[![Fika](https://img.shields.io/badge/Fika-2.3.x-purple)](https://github.com/project-fika/Fika-Plugin)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-yellow)](https://github.com/BepInEx/BepInEx)
![Deployment](https://img.shields.io/badge/deployment-singleplayer%2Chost_client%2Cheadless_all-lightgrey)

Заправка канистр топливом на АЗС и топливных базах на картах. Нативная игровая система взаимодействия с таймером и звуком.

| | |
|---|---|
| **Разработчик** | [kabzon93region](https://github.com/kabzon93region) |
| **Версия** | 1.2.3 |
| **GitHub** | [FuelStationRefill](https://github.com/kabzon93region/FuelStationRefill) |
| **Deployment** | `(singleplayer,host_client,headless_all)` |
| **Тип** | client |

## Возможности

- Заправка канистр топливом в определённых зонах на картах
- Нативная игровая система взаимодействия (аналог закладки квестовых предметов)
- Рандомизация топлива на станциях при старте рейда (каждый рейд уникален)
- Синхронизация через Fika (топливо общее для всех игроков)
- Звук заправки (generator repair loop)
- Целочисленное списание топлива со станции
- Режим разработчика с оверлеем координат (F9) и сохранением позиций (F10)
- Частичная заправка (прогресс сохраняется при прерывании)

## Как это работает

1. При старте рейда каждая станция получает случайное количество топлива (из диапазона `MinFuel`–`MaxFuel`)
2. Игрок подходит к зоне заправки (радиус из JSON + `InteractionRange`)
3. Если в инвентаре есть неполная канистра — появляется подсказка «Слить бензин в канистры [F]»
4. По нажатию F запускается нативное взаимодействие (как при закладке предмета в квестах)
5. Топливо начисляется по `RefuelRate` единиц в секунду, списание со станции — целыми единицами
6. Последняя неполная единица: канистра заполняется до максимума, станция списывает целую единицу
7. Если станция исчерпана — зона деактивируется до конца рейда

## Настройки (BepInEx Configuration Manager)

| Параметр | По умолчанию | Описание |
|---|---|---|
| `RefuelRate` | 2 | Скорость заправки (литров в секунду) |
| `InteractionRange` | 5 | Дистанция взаимодействия (метры) |
| `DefaultZoneRadius` | 1 | Радиус новой зоны при сохранении F10 (метры) |
| `MinFuel` | 0 | Минимум топлива на станции при рандомизации |
| `MaxFuel` | 40 | Максимум топлива на станции при рандомизации |
| `EnableFikaSync` | true | Синхронизация топлива через Fika |
| `DevMode` | false | Режим разработчика (оверлей координат) |
| `OverlayToggleKey` | F9 | Клавиша переключения оверлея |
| `SavePositionKey` | F10 | Клавиша сохранения позиции в JSON |

## Установка

1. Скачать zip-архив из [релизов](https://github.com/kabzon93region/FuelStationRefill/releases)
2. Распаковать в корень папки с игрой (`BepInEx/` из архива поверх `BepInEx/` игры)
3. Перезапустить SPT сервер и клиент

## Добавление зон заправки

Зоны хранятся в `BepInEx/plugins/FuelStationRefill/FuelStationZones.json`.

**Через режим разработчика:**
1. Включить `DevMode = true` в настройках мода
2. Зайти в рейд, встать в нужной точке
3. Нажать F10 — координаты сохранятся в JSON автоматически

**Вручную:**

```json
{
  "Zones": [
    {
      "Name": "АЗС",
      "Position": { "X": 322.7, "Y": 1.3, "Z": -176.9 },
      "Radius": 1.0,
      "MapName": "bigmap"
    }
  ]
}
```

| Поле | Описание |
|---|---|
| `Name` | Название зоны (для логов и оверлея) |
| `Position` | Координаты (X, Y, Z) |
| `Radius` | Радиус зоны в метрах |
| `MapName` | Код карты (`bigmap` = Таможня, `factory4_day` = Завод, и т.д.) |

## Fika (мультиплеер)

- Хост рандомизирует топливо при старте рейда и рассылает клиентам
- Расход топлива синхронизируется: если один игрок выкачал станцию, другие видят обновление
- Устанавливать мод нужно на **все клиенты и сервер**

## Совместимость

- SPT 4.0.13+
- BepInEx 5.4.x
- Fika 2.3.x

## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:
**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**

---

*Мод разработан при поддержке [Cursor AI](https://cursor.sh/) и Xiomi MiMo.*
