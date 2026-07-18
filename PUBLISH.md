# Publish to GitHub — Fuel Station Refill

**Статус:** `ready`  
**GitHub:** Release + zip  
**Версия:** `1.2.3`  
**Deployment:** `(singleplayer,host_client,headless_all)`

## 1. Подготовка (уже сделано этим скриптом)

Папка: `github-repos/FuelStationRefill/`

## 2. Создать репозиторий и запушить

```powershell
cd github-repos/FuelStationRefill
git init
git add .
git commit -m "Source backup Fuel Station Refill v1.2.3"
git branch -M main
git remote add origin https://github.com/kabzon93region/FuelStationRefill.git
git push -u origin main
```

Или автоматически:

```powershell
python CURSORAIMODING/tools/publish/publish_github_release.py FuelStationRefill --create-repo
```

## 3. GitHub Release

Прикрепить zip (только игровые файлы, без INSTALL.md):

`\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\FuelStationRefill_(singleplayer,host_client,headless_all)_v1.2.3_2026-07-19.zip`

```powershell
gh release create v1.2.3 "\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\FuelStationRefill_(singleplayer,host_client,headless_all)_v1.2.3_2026-07-19.zip" ^
  --title "Fuel Station Refill v1.2.3" ^
  --notes-file CHANGELOG.md
```

## Описание репозитория (suggested)

Заправка канистр топливом на АЗС и топливных базах на картах.

SPT 4.0 + Fika 2.3 headless stack. Deployment: `(singleplayer,host_client,headless_all)`.
