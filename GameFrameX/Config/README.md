# Luban 配置生成

编辑 `Excels/Local/L-Localization-找不同.xlsx`。该表包含找不同项目原有多语言数据和新增文案。

双击 `generate-client-json.bat`，或执行 `powershell -ExecutionPolicy Bypass -File .\generate-client-json.ps1`。工具先在项目 `Temp/LubanGenerated` 下生成并检查，再只更新本项目的多语言 JSON 和两个对应的 Luban 生成类；原文件备份保存在同一临时目录。启动页 5 条文案仍在 C# 中，修改表格不会更改启动页。

如果只想查看生成结果，不更新项目，执行 `powershell -ExecutionPolicy Bypass -File .\generate-client-json.ps1 -Preview`。此生成工程只维护多语言配置，不生成或覆盖其他游戏配置表。

运行需要 .NET 8。不要直接编辑 `Assets/Hotfix/Config/Generate` 下生成的文件。
