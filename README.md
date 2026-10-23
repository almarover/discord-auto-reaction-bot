# Discord Auto Reaction Bot

> 管理器现已使用 WinUI 3 原生版本；旧版统一归档在同级 `old` 文件夹，不会参与日常启动。

这是一个适合初学者的 Python Discord Bot。它只监听服务器里的**新消息**，支持多个 Discord 服务器和多个目标用户，也支持混合使用当前服务器 Custom Emoji、Bot 所在其他服务器的 Custom Emoji 与普通 Unicode Emoji。公共与专属 Reaction 都可以独立限定生效频道、选择全部添加或按精确概率随机抽取一个，并可设置一定概率不添加任何 Reaction。

它不会扫描历史消息，不使用数据库或 Web Dashboard，也不会读取、记录或分析消息正文。代码只使用非特权的 `Guilds` 和 `Guild Messages` Gateway Intents，**不需要开启 Message Content Intent**。

## 项目文件

```text
discord-auto-reaction-bot/
├── Discord Auto Reaction Bot.exe
├── bot.py
├── config_store.py
├── manager-winui/
├── dist/
├── start-bot.bat
├── build-bot-exe.bat
├── build-winui.bat
├── .env
├── .env.example
├── requirements.txt
├── requirements-build.txt
├── .gitignore
└── README.md
```

> 安全提示：`.env` 包含 Bot Token，已经被 `.gitignore` 排除。不要把 Token 发给别人，不要截图公开，也不要提交到 Git。

## 1. 在 Windows 安装 Python

1. 打开 [Python 官方 Windows 下载页](https://www.python.org/downloads/windows/)。
2. 下载当前受支持的 Python 3 版本（建议 Python 3.11 或更高版本）。
3. 运行安装程序，务必勾选 **Add python.exe to PATH**，然后选择 **Install Now**。
4. 安装完成后关闭并重新打开 PowerShell。
5. 验证安装：

```powershell
python --version
```

如果 `python` 不可用，可尝试 `py --version`。下面命令中的 `python` 也可相应替换为 `py`。

## 2. 创建 virtual environment

在 PowerShell 中进入本项目目录：

```powershell
cd "D:\Mine\Works\Discord Reaction Bot\discord-auto-reaction-bot"
python -m venv .venv
```

虚拟环境会把本项目的 Python 包与系统中其他项目隔离。

## 3. 激活 virtual environment

```powershell
.\.venv\Scripts\Activate.ps1
```

激活成功后，命令提示符前通常会显示 `(.venv)`。

如果 PowerShell 阻止脚本运行，可只对当前窗口临时放行，然后再次激活：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\.venv\Scripts\Activate.ps1
```

也可以使用命令提示符（Command Prompt）激活：

```bat
.venv\Scripts\activate.bat
```

## 4. 安装依赖

确认虚拟环境已激活，然后运行：

```powershell
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

本项目使用：

- `discord.py`：连接 Discord 和添加 Reaction。
- `python-dotenv`：从 `.env` 读取私密配置。
- `pystray`：提供 Windows 系统托盘图标和菜单。
- `Pillow`：生成托盘图标，并解码管理器中的 Emoji 缩略图。
- `emoji`：把 `:emoji_name:` 英文别名转换为真正的 Unicode Emoji。

## 5. 创建 Discord Application

1. 登录 [Discord Developer Portal](https://discord.com/developers/applications)。
2. 点击 **New Application**。
3. 输入应用名称，例如 `Auto Reaction Bot`。
4. 接受条款并创建应用。

## 6. 创建 Discord Bot

打开刚创建的 Application：

1. 左侧进入 **Bot** 页面。
2. 如果页面显示 **Add Bot**，点击并确认；如果已经显示 Bot 设置，则 Bot 用户已创建。
3. 可在这里修改 Bot 的用户名和头像。

## 7. 获取 Bot Token

在 **Bot** 页面找到 Token 区域：

1. 点击 **Reset Token**（或界面中用于查看/复制 Token 的按钮）。
2. 按要求完成验证。
3. 立即复制 Token，并把它填入本地 `.env` 的 `DISCORD_TOKEN=` 后面。

Token 相当于 Bot 的密码。若曾经泄露，请立即在 Portal 中重置，旧 Token 会失效。

## 8. 配置 Gateway Intents

在 Developer Portal 的 **Bot** 页面找到 **Privileged Gateway Intents**。

本项目**不需要开启其中任何开关**：

- 不开启 **Presence Intent**。
- 不开启 **Server Members Intent**。
- 不开启 **Message Content Intent**。

代码仅请求（可对照 [Discord 官方 Gateway Intents 文档](https://docs.discord.com/developers/events/gateway#gateway-intents)）：

- `Guilds`：知道 Bot 所在服务器，并获得频道/Emoji 等服务器信息。
- `Guild Messages`：收到服务器文字频道的新消息事件。

这两个属于普通（非 privileged）Intents，不需要在 Privileged Gateway Intents 区域打开开关。新消息事件本身带有发送者 ID，因此判断 `message.author.id` 不要求读取消息正文。

## 9. 按最小权限原则使用 Intents

`bot.py` 从 `discord.Intents.none()` 开始，只打开 `guilds` 和 `messages`。请不要为了“确保能运行”而在 Portal 或代码中开启所有 Intents。这个 Bot 不读取消息正文，也不需要成员列表、在线状态或 Direct Messages。

## 10. 创建 Bot Invite URL

在 Developer Portal 中打开应用：

1. 进入 **OAuth2** → **URL Generator**（某些新版界面也可在 **Installation** 页面配置 Guild Install）。
2. 在 Scopes 中只选择 **bot**。
3. 在 Bot Permissions 中选择下一节列出的最小权限。
4. 复制页面生成的 URL。

本项目不使用 Slash Commands，因此不需要 `applications.commands` scope。

## 11. Bot 所需的 Server Permissions

对需要监听和添加 Reaction 的频道，授予（可对照 [Discord 官方权限表](https://docs.discord.com/developers/topics/permissions#permissions-bitwise-permission-flags)）：

- **View Channels**：看见频道并接收其中的新消息。
- **Read Message History**：`discord.py` 的 `Message.add_reaction()` 所需。
- **Add Reactions**：添加新的 Reaction。

不需要 `Administrator`、`Manage Messages` 或 `Send Messages`。

如果只使用目标服务器自身上传的 Emoji，不需要 **Use External Emojis**。如果要使用 Bot 所在另一个服务器的 Emoji，Bot 在目标频道还必须拥有 **Use External Emojis（使用外部表情）** 权限；频道权限覆盖可能单独拒绝这项权限。

频道权限覆盖（channel overrides）可能覆盖服务器角色权限。请在目标频道的权限设置里确认 Bot 的角色没有被明确拒绝上述权限。Bot 也必须能访问配置的 Emoji；受角色限制、不可用或已删除的 Emoji 会记录失败并继续处理其余 Emoji。

## 12. 邀请 Bot 进入自己的 Discord Server

1. 在浏览器打开上一步复制的 Invite URL。
2. 选择你管理的服务器。
3. 检查权限，只保留所需权限。
4. 点击授权并完成人机验证。

你需要在该服务器拥有可邀请 Bot 的权限。邀请后，Bot 尚未运行时显示离线是正常现象。

## 13. 开启 Discord Developer Mode

在 Discord 桌面客户端：

1. 点击左下角齿轮进入 **User Settings**。
2. 进入 **Advanced**。
3. 打开 **Developer Mode**。

移动端的入口位置可能略有不同，通常也位于设置中的 Advanced。

## 14. 复制目标用户 User ID

开启 Developer Mode 后：

1. 在服务器成员列表或该用户的消息上右键。
2. 选择 **Copy User ID**。
3. 打开图形管理器，在对应服务器下点击“添加/更新用户”，填入这个数字 ID。

不要填写用户名、显示名或 `@mention`；必须是数字 User ID。

## 15. 配置服务器 Custom Emoji 与普通 Emoji

推荐方法：

1. 开启 Developer Mode。
2. 在消息中的 Custom Emoji 或 Emoji Picker 中右键该 Emoji。
3. 如果菜单提供 **Copy Emoji ID**，点击后得到纯数字 ID。
4. 对每个 Emoji 重复操作，并按想要添加 Reaction 的顺序用英文逗号连接。

如果客户端没有显示 Copy Emoji ID，可在输入框中键入反斜杠再选择/输入 Emoji，例如 `\:emoji_name:`，发送后通常会显示 `<:emoji_name:123456789012345678>`（动态 Emoji 可能是 `<a:emoji_name:...>`）；最后一段数字就是 Emoji ID。请只复制数字，不要复制名称、尖括号或冒号。

Custom Emoji 可以来自目标服务器，也可以来自 Bot 已加入的其他服务器。Bot 必须留在来源服务器并能访问该 Emoji；普通 Emoji 不受服务器来源限制。

普通 Emoji 没有数字 ID，可以直接从 Discord、系统 Emoji 面板或其他地方复制 Emoji 字符，例如 `👍`、`❤️`、`😂`。Windows 可以按 `Win + .` 打开系统 Emoji 面板。也可以填写英文 Emoji 别名，例如 `:thumbs_up:`、`:red_heart:`、`:face_with_tears_of_joy:`；管理器会自动把别名转换为真正的 Emoji。

管理器的公共和专属 Reaction 输入框支持两种格式混合填写，并可用英文逗号、空格或换行分隔。例如：

```text
1543554210203967510
:thumbs_up:
:red_heart:
:face_with_tears_of_joy:
```

Custom Emoji 请只填写数字 ID，不要写成 `<:名称:ID>`。`:emoji_name:` 只用于标准 Unicode Emoji 的英文名称，不能代表服务器自己上传的 Custom Emoji。保存并重新载入后，管理器会优先用可读别名显示普通 Emoji，避免 Windows 字体不支持时出现方框。

### 使用其他服务器的 Custom Emoji

跨服务器 Emoji 需要满足：

1. Bot 同时加入目标服务器和 Emoji 来源服务器。
2. 来源 Emoji 仍然存在且可用；如果它限制了角色，Bot 在来源服务器必须拥有允许使用的角色。
3. Bot 在目标频道拥有 **Use External Emojis（使用外部表情）** 权限。
4. Bot 必须能在目标频道查看消息、读取历史消息并添加 Reaction。

配置仍然只保存 Emoji ID。Bot 添加 Reaction 时会先搜索当前服务器，再搜索它已加入的其他服务器；跨服务器失败会在日志中显示来源服务器、Emoji ID、角色限制或缺少外部 Emoji 权限。Bot 离开来源服务器、Emoji 被删除或来源服务器失去 Emoji 可用资格后，该 Reaction 会失败，但不会影响列表中的其他 Reaction。

## 16. 填写 `.env` 与多服务器配置

用文本编辑器打开项目中的 `.env`：

```dotenv
DISCORD_TOKEN=你的_Bot_Token
TARGET_USER_ID=
TARGET_EMOJI_IDS=
```

示例（这些是假 ID，不能直接使用）：

```dotenv
DISCORD_TOKEN=replace_with_your_real_token
TARGET_USER_ID=
TARGET_EMOJI_IDS=
```

新版本只要求 `.env` 中有 `DISCORD_TOKEN`。目标用户和 Emoji 由图形管理器按服务器保存到本地 `.bot-config.json`；该文件已被 `.gitignore` 排除。

如果 `.env` 中仍有旧的 `TARGET_USER_ID` 和 `TARGET_EMOJI_IDS`，管理器第一次同步服务器时会自动迁移：旧 Emoji 成为公共 Reaction，旧用户成为该服务器的第一个目标用户。迁移后请在管理器中维护配置。

## 17. 启动图形管理器和 Bot

推荐直接双击项目文件夹第一页的：

```text
Discord Auto Reaction Bot.exe
```

管理器会启动 Bot、同步它已经加入的 Discord 服务器，并在下拉列表中显示服务器名称和 Server ID。首次同步后：

1. 选择一个服务器。
2. 填写公共 Reaction：Custom Emoji 使用数字 ID；普通 Emoji 可直接粘贴或填写 `:emoji_name:` 英文别名；可使用逗号、空格或换行分隔。公共页面还可选择生效频道、随机抽取概率和“不进行 Reaction”概率。
3. 添加一个或多个目标用户。
4. 每个用户可单独填写专属 Reaction，也可以留空，只使用公共 Reaction；专属 Reaction 可独立选择频道、随机抽取和“不进行 Reaction”概率。
5. 点击“保存当前服务器配置”。Bot 会在下一条新消息时自动加载，无需重启。

也可以在 PowerShell 中直接运行 Bot，但这样没有托盘管理和图形配置：

```powershell
cd "D:\Mine\Works\Discord Reaction Bot\discord-auto-reaction-bot"
.\.venv\Scripts\Activate.ps1
python bot.py
```

连接成功后日志会显示 Bot 用户名、Bot User ID、已连接服务器，以及当前配置的服务器和目标用户数量。

当目标用户在 Bot 可访问的服务器文字频道中发送**新消息**时，Bot 会按配置顺序逐个添加 Reaction。每个 Reaction 都独立处理：某一个失败只会打印原因，不会阻止后面的 Emoji，也不会让 Bot 崩溃。终端不会输出消息正文。

## 18. 停止 Bot

在管理器窗口或系统托盘菜单点击 **停止 Bot**。如果是手动在 PowerShell 中运行，则按：

```text
Ctrl+C
```

关闭终端或让电脑休眠也会使 Bot 离线。若要 24 小时在线，需要把项目部署到持续运行的主机；本教程只介绍本地运行。

## 19. 常见错误排查

### `python` 不是可识别的命令

重新安装 Python 并勾选 **Add python.exe to PATH**，然后重开 PowerShell；或尝试用 `py` 替代 `python`。

### PowerShell 无法激活 `.venv`

运行：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

这只影响当前 PowerShell 窗口。然后再次执行激活命令。

### `ModuleNotFoundError: No module named 'discord'` 或 `'dotenv'`

确认提示符前有 `(.venv)`，再运行：

```powershell
python -m pip install -r requirements.txt
```

不要安装名为 `discord` 的其他包；本项目需要的是 `discord.py`。

### `[Configuration Error]`

按错误提示检查 `.env`。文件名必须正好是 `.env`，不能是 `.env.txt`，并确认 `DISCORD_TOKEN=` 后有真实 Token。目标用户和 Emoji 的格式错误会由管理器在保存前指出。

### `[Login Error] Discord rejected DISCORD_TOKEN`

Token 不正确、已重置或复制不完整。回到 Developer Portal 的 **Bot** 页面重置并复制新 Token，然后更新 `.env`。不要把 OAuth2 Client Secret 或 Application ID 当作 Bot Token。

### Bot 显示在线，但没有响应目标用户

- 在管理器中选择消息所在的正确服务器，并确认目标用户已加入该服务器配置。
- 确认填写的是用户 ID，而不是服务器 ID、用户名或 Bot ID。
- Bot 只处理启动后收到的新服务器消息，不处理私信和历史消息。
- 确认 Bot 能看见该频道，且频道权限没有拒绝它。
- 修改服务器、用户或 Emoji 配置后点击保存；Bot 会自动加载。
- 不需要开启 Message Content Intent；本程序不读取正文。

### `[Reaction Failed] ... Emoji was not found`

- 检查 Emoji ID 是否复制正确。
- 确认 Emoji 尚未被删除，并来自 Bot 已加入且可访问的服务器。
- 确认该 Emoji 没有角色限制或其他可用性限制。
- Bot 会先搜索所有已连接服务器的缓存；仍找不到时，会依次向 Discord 查询 Bot 已加入的服务器。

### `[Reaction Failed] ... Discord denied access`

检查服务器角色和目标频道的权限覆盖，确保 Bot 拥有 **View Channels**、**Read Message History** 和 **Add Reactions**。跨服务器 Emoji 还需要 **Use External Emojis**。不要用 Administrator 来掩盖权限配置问题。

### 部分 Reaction 成功，部分失败

这是预期的容错行为。查看每条 `[Reaction Failed]` 的 Emoji ID 和原因，修复对应 Emoji 或权限；程序仍会继续尝试列表中的剩余 Emoji，并在最后显示成功数量。

### Bot 重连后再次打印 `[Bot Ready]`

网络中断后 `discord.py` 会自动尝试重连，`on_ready` 可能再次触发。这不代表启动了多个 Bot 实例。

## 工作方式摘要

1. Discord 发送一条新的服务器消息事件。
2. Bot 使用消息的 Server ID 查找该服务器独立配置。
3. Bot 忽略私信、Bot 账号消息、未启用的服务器和未配置的用户。
4. 对目标用户，分别检查公共与专属 Reaction 的频道范围；两组规则互不覆盖，未选择的频道不会执行对应的一组 Reaction。
5. 公共和专属 Reaction 各自支持全部添加、按精确百分比随机抽取一个，以及按设定概率不添加该组 Reaction；最终结果按顺序去重。
6. 每个 Reaction 单独捕获权限、资源不存在、Discord API 和其他异常；单个失败不会阻止后续 Reaction。

## Windows 图形管理器

新版原生管理器位于 `manager-winui`，采用 WinUI 3 深色左侧导航界面，并继续读写原项目中的 `.env`、`.bot-config.json` 和 `.bot-runtime.json`。页面包括：

- **主页**：Token、Bot 状态、服务器数量与详情、启动、停止、最小化和退出。
- **公共**：按服务器启用或关闭自动 Reaction，选择生效频道，并编辑、预览公共 Reaction 及其随机规则。
- **配置**：按服务器维护目标用户、专属 Reaction、生效频道、逐项预览、随机抽取和精确概率。
- **日志**：查看、筛选和清空当前管理器捕获的日志，不输出用户消息正文。
- **设置**：Windows 自启动、自动启动 Bot、启动时隐藏和关闭到托盘。

服务器 Emoji 库暂时不提供。Custom Emoji 直接填写数字 ID；跨服务器 ID 的 Bot 后端能力仍然保留。

新版管理器使用原生 Win32 托盘图标并限制为单实例。双击托盘图标恢复窗口；右键菜单可以打开管理器、启动 Bot、停止 Bot或退出管理器。旧版文件已集中到同级 `old` 文件夹，只作归档。

### Windows EXE 版本

构建 WinUI 管理器：

```powershell
.\build-winui.bat
```

实际运行文件输出位置：

```text
dist\winui-manager\DiscordReactionManager.WinUI.exe
```

该输出为自包含 WinUI 文件夹，旁边的 DLL 是其运行依赖，不能单独移动。因此项目根目录额外提供带机器人图标的 `Discord Auto Reaction Bot.exe` 作为日常启动入口；它会自动打开实际管理器。目标电脑不需要另行安装 .NET。管理器继续启动 `dist\DiscordReactionBot.exe`，保存配置后 Bot 会在下一条新消息时自动重新加载。

## 频道范围与随机模式

公共页面和每个目标用户的配置中都有“选择频道”。默认的“全部可访问文字频道”与旧版本行为一致；关闭它后必须至少勾选一个频道。公共频道范围只控制公共 Reaction，用户频道范围只控制该用户的专属 Reaction。

公共和专属 Reaction 分别提供两层独立决策：

1. 可选的“不进行 Reaction”概率。例如设为 `50%`，该组 Reaction 有一半概率直接跳过。
2. 未被跳过时，可以添加该组全部 Reaction，也可以开启随机抽取并按每项权重只选一个。

因此，开启“不进行 Reaction”但关闭“随机抽取”时，触发后会添加该组配置的全部 Reaction；两个开关同时开启时，先判断是否跳过，再从各 Reaction 中抽取一个。

## 每用户随机抽取模式

随机功能默认关闭，因此旧配置仍会按顺序添加全部 Reaction。要为某个用户开启：

1. 在服务器下选择该目标用户，或填写一个新用户 ID。
2. 打开“随机抽取与概率”标签页。
3. 打开“从专属 Reaction 中随机抽取 1 个”。
4. 概率表只列出该用户的专属 Emoji；公共 Reaction 使用公共页面自己的独立设置。
5. 在每个 Reaction 右侧直接输入百分比，支持整数和小数。
6. 也可以点击“一键均分”把全部 Reaction 分成 100%，最后一项会吸收舍入差额。
7. 点击“添加 / 更新用户”保存。

所有专属 Emoji 的设定百分比必须合计为 `100%`，否则管理器会指出当前合计并阻止保存该目标用户。例如三个 Emoji 可以设置为 `33.33%`、`33.33%`、`33.34%`；也可以把不希望抽中的 Emoji 设为 `0%`。

概率配置是“每个目标用户的专属 Reaction 独立”。保存后 Bot 会在下一条目标消息时自动读取，无需重启。
