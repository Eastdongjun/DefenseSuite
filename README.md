# DefenseSuite v1.0

**Windows Server 攻击防御系统** — 历经生产环境数月实战验证，一键安装四层防护。

## 快速开始

```batch
# 安装（右键以管理员运行）
DefenseSuite-Setup.bat

# 查看状态
DefenseSuite-Setup.bat /status

# 静默安装
DefenseSuite-Setup.bat /silent "1.2.3.4,5.6.7.8"

# 卸载
DefenseSuite-Setup.bat /uninstall
```

## 四层防线

| 组件 | 机制 | 触发方式 |
|------|------|----------|
| **AutoDefender** | 监听 Security 4625 失败登录 → Tier1/2/3 封禁 | 每 3 分钟 |
| **Honeypot** | 12 个假服务端口 → 触碰即封 /24 子网 | 开机自启 |
| **WebTrap** | 监控 Web 访问日志 → 扫描路径命中即封 | 开机自启 |
| **QuickResponse** | 实时监听 4625 事件 → 秒级封禁 | 开机自启 |

## 12 个蜜罐端口

3389(假RDP), 22(假SSH), 23(假Telnet), 21(假FTP), 5900(假VNC), 9200(假ES), 11211(假Memcached), 27017(假MongoDB), 8088(假Hadoop), 5432(假PG), 5555(假ADB), 8443(假HTTPS管理)

## 分级封禁

| Tier | 阈值 | 动作 |
|------|------|------|
| 1 | 2 次失败 | 封单 IP 24h |
| 2 | 5 次失败 | 封 /24 子网 |
| 3 | 10 次失败 | 永久封禁 + /16 子网 |

## 系统要求

- Windows Server 2016/2019/2022
- PowerShell 5.1+
- 管理员权限

## 安装位置

- 程序：`C:\Program Files\DefenseSuite\`
- 日志：`C:\ProgramData\DefenseSuite\logs\`
- 计划任务：`DefenseSuite-AutoDefender` / `Honeypot` / `WebTrap` / `QuickResponse`

## 构建 EXE

```powershell
# 需要 .NET Framework 4.x (系统自带)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:exe /out:DefenseSuite-Setup.exe Bootstrapper.cs

# 分发时将 EXE 和 .ps1 放在同一目录
```

## License

MIT
