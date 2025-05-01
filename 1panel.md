# 🚀 TelegramStickerPorter - 1Panel 部署教程

> 📌 适用于：使用 1Panel 面板的 VPS / 云服务器用户，无需命令行操作！

---

## 🧩 第一步：准备工作

#### ✅ 你需要准备：

1. 一台安装了 [1Panel 面板](https://1panel.cn/) 的服务器（推荐 Ubuntu / Debian）
2. 一个已申请好的 Telegram Bot（通过 [@BotFather](https://t.me/BotFather) 获取 Bot Token）
3. 下载与你服务器架构匹配的发布包：
   👉 [点击前往 Releases 页面下载](https://github.com/Riniba/TelegramStickerPorter/releases)

---

## 🛠 第二步：上传并解压程序

1. 登录 1Panel 面板后台
2. 左侧点击「**文件管理器**」
3. 进入你希望部署的目录（推荐路径：`/opt/telegram-sticker-porter`）
4. 上传你下载的 zip 包（如 `TelegramStickerPorter-linux-x64.zip`）
5. 解压 zip 包，解压后路径如下所示：

```
/opt/telegram-sticker-porter/TelegramStickerPorter
```

---

## ✏️ 第三步：配置 BotToken

1. 找到并编辑以下文件：

```
/opt/telegram-sticker-porter/TelegramStickerPorter/appsettings.json
```

2. 修改内容如下：

```json
{
  "Telegram": {
    "BotToken": "123456:ABCDEF_YourRealTokenHere"
  }
}
```

✅ 替换为你自己的 BotToken  
✅ 修改完后保存！

---

## 🔐 第四步：设置权限（使用 1Panel 后台）

为确保程序可执行，需设置运行权限：

1. 回到 1Panel 后台 → 左侧点击「**文件管理器**」
2. 进入目录 `/opt/telegram-sticker-porter/TelegramStickerPorter/`
3. 选中所有文件，点击右侧「更多操作 → 权限」
4. 设置如下：

| 项目     | 设置值 |
| -------- | ------ |
| 权限     | `0777` |
| 所属用户 | `root` |
| 用户组   | `root` |

5. 点击「确定」保存权限

> ✅ 所有子文件和子目录将自动继承权限

---

## 🧭 第五步：使用进程守护（Supervisor）启动 Bot

1. 在 1Panel 左侧导航栏点击 **服务 > 进程守护（Supervisor）**
2. 点击右上角「创建守护进程」
3. 填写以下配置：

| 项目     | 内容                                                         |
| -------- | ------------------------------------------------------------ |
| 名称     | `TelegramStickerPorter`                                      |
| 启动用户 | `root`                                       |
| 启动命令 | `/opt/telegram-sticker-porter/TelegramStickerPorter/./TelegramStickerPorter` |
| 启动目录 | `/opt/telegram-sticker-porter/TelegramStickerPorter`         |
| 进程数量 | `1`                                                     |


4. 点击「确认」，然后在守护进程列表中点击「启动」

---

## 📄 第六步：查看运行状态

- 点击服务右侧的「日志」图标
- 查看程序是否成功启动，是否连接 Telegram 成功

---

## ✅ 成功效果

- 打开 Telegram 给你的机器人发送 `/start`
- 会收到操作说明
- 然后发送贴纸/表情克隆命令，即可自动搬运！

---

## ❓ 常见问题

| 问题        | 解决方式                                               |
| ----------- | ------------------------------------------------------ |
| 没反应？    | 检查 `appsettings.json` 中的 `BotToken` 是否正确       |
| 启动失败？  | 查看 Supervisor 日志，看是否路径不对或者权限不对等     |

---

## 🧩 后续建议

- ✅ 支持将本项目设置为 1Panel 应用商店中的一键部署包
- ✅ 支持使用 Docker 镜像自动部署（需要我也可以帮你写）
- ✅ 可加入 Web 控制台，让小白更好地控制贴纸克隆任务

---

> 📬 有问题欢迎到 [项目 GitHub](https://github.com/Riniba/TelegramStickerPorter/issues) 提 Issue，我们会持续优化部署体验！
