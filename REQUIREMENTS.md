# DefenseSuite v1.0 — 需求规格说明书

## 项目背景

基于 Windows Server 2019 生产环境数月实战验证的四层防御体系，将其产品化为通用的 Windows 服务器安全防护软件。

## 核心需求

### R1 — 一键安装部署
- 管理员运行 EXE 后自动完成：文件释放、计划任务创建、防火墙初始化、组件启动
- 支持静默安装 `/S` 参数，适用批量部署
- 安装后自动检测端口冲突，跳过被业务占用的蜜罐端口

### R2 — 三层自动封禁
- **Tier 1**: 2 次失败登录 → 封禁单 IP 24 小时
- **Tier 2**: 5 次失败登录 → 封禁 /24 子网 (256 IP)
- **Tier 3**: 10 次失败登录 → 永久封禁 + /16 子网封禁
- Lookback 窗口 6 小时，防止慢速扫描绕过
- 历史计数持久化，过期解封的 IP 再次攻击时累计升级

### R3 — TCP 端口蜜罐
- 默认 12 个陷阱端口：3389(假RDP), 22(假SSH), 23(假Telnet), 21(假FTP), 5900(假VNC), 9200(假ES), 11211(假Memcached), 27017(假MongoDB), 8088(假Hadoop), 5432(假PG), 5555(假ADB), 8443(假HTTPS管理)
- 任何外部 IP 连接陷阱端口 → 立即封禁 IP + /24 子网
- 返回假横幅消耗攻击者时间

### R4 — Web 路径陷阱
- 监控 HTTP 端口的高危扫描路径
- 命中陷阱路径 → 自动封禁 IP + /24 子网
- 默认陷阱路径：phpmyadmin, .env, .git, wp-admin, wp-login, actuator, solr, jenkins 等 20+ 条

### R5 — 秒级应急响应
- 监听 Windows Security Event 4625
- 攻击事件发生后秒级封禁（不等待定时扫描）
- 与计划任务 AutoDefender 互补：事件触发 + 定时扫荡

### R6 — 完整卸载
- 移除所有防火墙规则（DEFENDER_BLOCK_ / HONEYPOT_ / WEBTRAP_ 前缀）
- 删除所有计划任务
- 清理日志和程序文件
- 卸载后不留痕迹

### R7 — 状态查看
- `DefenseSuite.exe /status` 显示：
  - 计划任务运行状态
  - 蜜罐端口监听数量
  - 防火墙封禁规则统计
  - 近期攻击触发记录
  - 白名单配置

### R8 — 白名单管理
- 安装时可添加白名单 IP
- 运行时可通过 CLI 添加/删除
- 白名单 IP 永不封禁（即使触发陷阱）
- 内网 IP (10.x, 172.16-31.x, 192.168.x, 127.x) 自动白名单

### R9 — 配置驱动
- 所有参数通过 config.json 配置
- 支持自定义端口列表、阈值、日志路径
- 配置文件注释清晰，管理员可直接编辑

### R10 — 生成 EXE
- 使用 ps2exe 打包为独立 EXE
- 不依赖外部运行时（PowerShell 5.1 系统自带）
- 目标平台：Windows Server 2016/2019/2022

## 非需求（不做）

- ❌ 不开发 GUI 界面（服务器无桌面环境）
- ❌ 不依赖第三方杀毒软件
- ❌ 不修改业务应用的 Web 服务器配置（Nginx 陷阱为可选集成）
- ❌ 不支持 Linux（仅 Windows）

## 文件结构

```
DefenseSuite/
├── DefenseSuite.psm1              # 核心模块
├── config.json                    # 默认配置
├── components/
│   ├── auto_defender.ps1          # 失败登录监控
│   ├── honeypot.ps1               # TCP 蜜罐
│   ├── web_trap_watcher.ps1       # Web 路径陷阱
│   └── quick_response.ps1         # 事件触发响应
├── build.ps1                      # 构建 EXE 脚本
├── install.bat                    # 快捷安装入口
└── REQUIREMENTS.md                # 本文档
```

## 安装后产物

| 类型 | 位置 |
|------|------|
| 程序文件 | `C:\Program Files\DefenseSuite\` |
| 日志 | `C:\ProgramData\DefenseSuite\logs\` |
| 计划任务 | `DefenseSuite-AutoDefender` / `DefenseSuite-Honeypot` / `DefenseSuite-WebTrap` / `DefenseSuite-QuickResponse` |
| 防火墙规则 | `DEFENDER_BLOCK_*` / `HONEYPOT_*` / `WEBTRAP_*` |
