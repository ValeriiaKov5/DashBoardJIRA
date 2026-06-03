# Jira Sprint Dashboard (Windows EXE)

Приложение для мониторинга спринта в Jira с двумя разделами:
- `Настройки`
- `Основное`

## Что умеет

- Подключение к **Jira Cloud** (Bearer API-токен) и **Jira Server / Data Center** (логин + пароль).
- Выбор проекта Jira по наименованию или ключу.
- Загрузка и выбор спринта.
- Подбор команды проекта из пользователей Jira.
- Основные метрики спринта:
  - всего задач
  - задач в работе
  - задач выполнено
  - план в часах
  - факт в часах
  - процент факт/план
- Фильтр по каждому члену команды.
- Таблица по задачам спринта:
  - плановое время (сумма `timeoriginalestimate` по подзадачам)
  - фактическое время (сумма `worklog.timeSpentSeconds` по задаче)

## Настройка Jira

### Jira Cloud (`*.atlassian.net`)

| Поле | Значение |
|------|----------|
| Jira URL | `https://your-company.atlassian.net` |
| Логин | оставьте пустым |
| Пароль или API-токен | API-токен (Bearer) |

### Jira Server / Data Center (например `https://jira.sminex.com`)

| Поле | Значение |
|------|----------|
| Jira URL | `https://jira.sminex.com` |
| Логин | ваш логин в Jira (**обязательно**) |
| Пароль или API-токен | пароль или персональный токен |
| Наименование проекта | точное имя или ключ (`DEV`) |

После заполнения:
1. Нажмите **Загрузить проект, спринты и команду**.
2. Выберите спринт и участников команды.
3. Нажмите **Сохранить настройки**.
4. Перейдите в **Основное** и нажмите **Обновить**.

## Сборка EXE на GitHub (без установки .NET на вашем ПК)

### Структура репозитория (важно)

В **корне** репозитория на GitHub должны лежать файлы напрямую:

```
JiraSprintDashboard.csproj
Program.cs
MainForm.cs
...
.github/workflows/build.yml
```

**Неправильно:** папка `JiraSprintDashboard/JiraSprintDashboard.csproj` внутри ещё одной папки (частая ошибка при upload).

### Запуск сборки

1. Загрузите все файлы проекта (включая `.github/workflows/build.yml`).
2. **Settings** → **Actions** → **General** → Allow all actions.
3. Вкладка **Actions** → **Build Windows EXE** → **Run workflow**.
4. Откройте запуск с зелёной галочкой → **Artifacts** → `JiraSprintDashboard-exe`.

### Если сборка красная (failed)

1. Откройте failed run → шаг с красным крестиком.
2. Типичные причины:
   - **JiraSprintDashboard.csproj не найден** — файлы не в корне репозитория.
   - **EXE не создан** — не все `.cs` файлы загружены.
   - **Actions отключены** — включите в Settings.
3. Обновите на GitHub файл `.github/workflows/build.yml` из этого проекта и запустите **Run workflow** снова.

## Сборка EXE локально

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download) на машине сборки.

```powershell
.\publish-exe.ps1
```

Готовый файл:

`bin\Release\net8.0-windows\win-x64\publish\JiraSprintDashboard.exe`

EXE можно запускать на Windows x64 без установки .NET Runtime.

## Запуск из Visual Studio

1. Откройте `JiraSprintDashboard.csproj`.
2. Нажмите **F5**.

## Где хранятся настройки

`%AppData%\JiraSprintDashboard\settings.json`

## Типичные ошибки

| Ошибка | Причина |
|--------|---------|
| `'<' is an invalid start of a value` | Jira вернула HTML вместо JSON: неверный URL, логин/пароль или API v3 на Server |
| Проект не найден | Укажите точное имя проекта из Jira |
| Не найдена scrum/kanban доска | У проекта должна быть agile-доска |
