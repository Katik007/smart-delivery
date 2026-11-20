# Simple Project

Приложение ASP.NET Core (.NET 10) для создания, редактирования и просмотра заявок.
Заявки хранятся в PostgreSQL. Таблица создаётся при первом запуске приложения.

## Локальный запуск через Docker Compose

В PowerShell:

```powershell
docker build -t simple-project:local .
docker network inspect nginx-proxy 2>$null
if ($LASTEXITCODE -ne 0) { docker network create nginx-proxy }
$env:APP_IMAGE = 'simple-project:local'
if (-not (Test-Path .postgres-password)) {
    [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) |
        Set-Content .postgres-password
}
$env:POSTGRES_PASSWORD = (Get-Content .postgres-password -Raw).Trim()
docker compose run --rm -p 127.0.0.1:8080:8080 app
```

Откройте http://localhost:8080. Остановите приложение через Ctrl+C и выполните
`docker compose down`. Том `postgres-data` сохранится; для повторного запуска
используйте прежний пароль. `docker compose down -v` удалит базу вместе с заявками.

## GitLab CI и Container Registry

Pipeline собирает образ через Docker-in-Docker, отправляет его в Container Registry этого проекта и выполняет деплой:

- Каждый коммит ветки: `$CI_REGISTRY_IMAGE:$CI_COMMIT_SHA`.
- Основная ветка: дополнительно `$CI_REGISTRY_IMAGE:latest`.

Логин выполняется через встроенные переменные `CI_REGISTRY`, `CI_REGISTRY_USER` и `CI_REGISTRY_PASSWORD`; добавлять пароль вручную не требуется. Container Registry должен быть включён в настройках проекта.

Для задачи `docker-build` нужен Linux runner с Docker executor. В его `config.toml` настройте секцию существующего раннера и перезапустите его:

```toml
[runners.docker]
  privileged = true
  volumes = ["/certs/client", "/cache"]
```

Если у раннера есть теги, добавьте соответствующий `tags` в обе задачи `.gitlab-ci.yml`. Без тегов включите у раннера Run untagged jobs.

Посмотреть образы: GitLab → Deploy → Container Registry.

После успешного pipeline основной ветки:

```sh
docker login registry.gitlab.com
docker pull registry.gitlab.com/lessons2530643/simple-project:latest
```

Для запуска этого образа используйте инструкцию Compose выше, указав в `APP_IMAGE`
`registry.gitlab.com/lessons2530643/simple-project:latest` вместо локального тега.
Для приватного реестра при локальном входе используйте GitLab-токен с правом `read_registry`.

## Деплой по SSH

После успешной сборки основной ветки задача `deploy-production` автоматически
деплоит образ текущего коммита на `root@136.234.238.30` через Docker Compose.
Все команды деплоя находятся в `.gitlab-ci.yml`, отдельного скрипта нет.
В `/home/vlasovandrey` копируется `compose.yaml`;
задача сохраняет точный образ с тегом коммита в `.env`, скачивает его и выполняет
`docker compose up -d --wait --wait-timeout 60`.
Исходники и .NET SDK на сервере не нужны: приложение находится внутри образа.

Compose запускает PostgreSQL в сервисе db, а данные сохраняет в томе postgres-data. При первом деплое CI генерирует пароль и сохраняет его в .postgres-password в каталоге деплоя. Не удаляйте этот файл, пока используется существующий том БД.

Проброса портов на сервер нет. Приложение слушает порт 8080 внутри контейнера,
но через `http://сервер:8080` недоступно. URL в GitLab environment не задан.
Compose ожидает запуска контейнера; проверки HTTP и автоматического отката нет.
При замене контейнера будет короткий перерыв.

Подготовка Linux-сервера:

- Docker должен быть установлен и запущен, плагин Docker Compose должен поддерживать `up --wait`.
- Добавить публичный SSH-ключ в `/root/.ssh/authorized_keys`.
- Разрешить SSH-вход root по ключу, доступ раннера к порту 22.
- SSH-пользователь должен иметь доступ к Docker и право записи в `/home/vlasovandrey`.
- Сервер должен иметь доступ к registry.gitlab.com для скачивания образа.

В Settings → CI/CD → Variables добавить переменную типа **File**:

| Key | Value |
| --- | --- |
| `SSH_PRIVATE_KEY` | Приватный ключ целиком, с переводом строки в конце; Visibility: Visible |

Переменную рекомендуется сделать Protected, основную ветку — protected.
Если задан Environment scope, используйте `production` или `*`.
Не включайте debug trace для задачи с приватным ключом.

Проверка подлинности SSH-сервера отключена (`StrictHostKeyChecking=no`,
`UserKnownHostsFile=/dev/null`), поэтому соединение уязвимо к подмене сервера.

Registry-пароль берётся из встроенной переменной CI_REGISTRY_PASSWORD,
передаётся через SSH stdin и хранится на сервере во временном Docker config,
который удаляется после задачи. Отдельный registry-токен не нужен.

## Подключение Nginx Proxy Manager

На сервере создайте общую сеть (если она ещё не создана) и подключите уже запущенный контейнер:

```sh
docker network inspect nginx-proxy >/dev/null 2>&1 || docker network create nginx-proxy
docker network connect nginx-proxy nginx-proxy-manager
```

В Nginx Proxy Manager добавьте Proxy Host: схема `http`, имя `vlasovandrey`,
порт `8080`. Приложение присоединяется к общей сети при деплое, PostgreSQL
остаётся в сети проекта. Имя приложения в общей сети совпадает с именем папки деплоя.

## Отдельные проекты в /home/name

В другом репозитории измените `DEPLOY_DIR` в `.gitlab-ci.yml` на `/home/name`
или задайте эту переменную в GitLab → Settings → CI/CD → Variables.
Имя папки должно начинаться с маленькой латинской буквы или цифры и содержать
только `a-z`, `0-9`, `_`, `-`. Для каждого проекта используйте отдельную папку.
При необходимости измените `SSH_HOST`, `SSH_USER` и настройте `SSH_PRIVATE_KEY`.
Образ берётся из Container Registry соответствующего репозитория автоматически.

Имя проекта Compose определяется папкой (`vlasovandrey`, `name` и т. д.).
Фиксированного `container_name` и опубликованных портов нет, поэтому разные
проекты получают отдельные контейнеры и сети без конфликта имён и портов.

Управление на сервере:

```sh
cd /home/vlasovandrey
docker compose ps
docker compose logs -f
docker compose up -d
docker compose down
```

Для скачивания приватного образа вручную сначала выполните `docker login registry.gitlab.com`
с токеном `read_registry`: временные учётные данные CI удаляются после деплоя.
