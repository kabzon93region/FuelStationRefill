# Fuel Station Refill

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.2.3-blue)](https://github.com/kabzon93region/FuelStationRefill/releases/tag/v1.2.3)
[![Download zip](https://img.shields.io/badge/download-zip-brightgreen)](https://github.com/kabzon93region/FuelStationRefill/releases/download/v1.2.3/FuelStationRefill_(singleplayer,host_client,headless_all)_v1.2.3_2026-07-19.zip)
[![EFT](https://img.shields.io/badge/EFT-16.9-orange)](https://www.escapefromtarkov.com/)
[![SPT](https://img.shields.io/badge/SPT-4.0.13-blue)](https://sp-tarkov.com/)
[![Fika](https://img.shields.io/badge/Fika-2.3.x-purple)](https://github.com/project-fika/Fika-Plugin)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-yellow)](https://github.com/BepInEx/BepInEx)
![Deployment](https://img.shields.io/badge/deployment-singleplayer%2Chost_client%2Cheadless_all-lightgrey)

Заправка канистр топливом на АЗС и топливных базах на картах.

| | |
|---|---|
| **Разработчик** | [kabzon93region](https://github.com/kabzon93region) |
| **Версия** | 1.2.3 |
| **GitHub** | [FuelStationRefill](https://github.com/kabzon93region/FuelStationRefill) |
| **Deployment** | `(singleplayer,host_client,headless_all)` |
| **Тип** | client |
## Возможности

- Заправка канистр топливом в определённых зонах на картах
- Нативная игровая система взаимодействия (как закладка квестовых предметов)
- Рандомизация топлива на станциях при старте рейда
- Синхронизация через Fika (топливо общее для всех игроков)
- Звук заправки (generator repair loop)
- Целочисленное списание топлива со станции
- Режим разработчика с оверлеем координат (F9) и сохранением позиций (F10)
## Настройки (BepInEx Configuration Manager)

| Параметр | По умолчанию | Описание |
|---|---|---|
| `RefuelRate` | 2 | Скорость заправки (л/с) |
| `InteractionRange` | 5 | Дистанция взаимодействия (м) |
| `DefaultZoneRadius` | 1 | Радиус новой зоны (м) |
| `MinFuel` / `MaxFuel` | 0 / 40 | Диапазон рандомизации топлива |
| `EnableFikaSync` | true | Синхронизация через Fika |
| `DevMode` | false | Режим разработчика |
## Установка

1. Скопировать `BepInEx/` из архива в корень игры
2. Зоны заправки определяются в `BepInEx/plugins/FuelStationRefill/FuelStationZones.json`
3. Для добавления новых зон: включить `DevMode`, зайти в рейд, встать в нужной точке, нажать F10
## Требования

- SPT 4.0.13+
- BepInEx 5.4.x
- Fika 2.3.x (для мультиплеера)
## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:
**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**
