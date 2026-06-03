# Как загрузить проект, чтобы сборка прошла

## Главная ошибка: папка `.github` не загружается

В Windows папки, начинающиеся с точки, **скрытые**. При перетаскивании файлов в GitHub они **часто не попадают** в репозиторий. Без них сборка не запускается или падает.

### Как правильно добавить workflow

1. Откройте репозиторий на GitHub.
2. Нажмите **Add file** → **Create new file**.
3. В поле имени файла введите **точно** (скопируйте):

   ```
   .github/workflows/build.yml
   ```

4. GitHub сам создаст папки `.github` и `workflows`.
5. Скопируйте содержимое файла `build.yml` из папки проекта на диске.
6. **Commit changes**.

## Файлы в корне репозитория

На вкладке **Code** должно быть **без лишней вложенной папки**:

```
JiraSprintDashboard.csproj
Program.cs
MainForm.cs
JiraClient.cs
Models.cs
SettingsStore.cs
README.md
.github/workflows/build.yml
```

Неправильно:

```
JiraSprintDashboard/
  JiraSprintDashboard/
    JiraSprintDashboard.csproj
```

## Запуск сборки

1. **Settings** → **Actions** → **General** → Allow all actions → Save.
2. **Actions** → **Build Windows EXE** → **Run workflow**.

## Если снова failed

1. Откройте красный запуск.
2. Шаг **Show repository files** — есть ли `.csproj` и все `.cs`?
3. Шаг **Publish EXE** — скопируйте текст ошибки (последние 20 строк).

Частые сообщения:

| Ошибка | Решение |
|--------|---------|
| `Missing JiraSprintDashboard.csproj` | Загрузите `.csproj` в корень |
| `Could not find file MainForm.cs` | Загрузите все 5 файлов `.cs` |
| Нет workflow в Actions | Создайте `.github/workflows/build.yml` вручную (см. выше) |
| `No jobs` | Actions отключены в Settings |
