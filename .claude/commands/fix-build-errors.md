---
description: Fix build errors from auto-built-errors.log, rebuild until clean
argument-hint: "[projectId: 3161035078]"
---
Project: $ARGUMENTS (if empty, `3161035078`). ModLib (`GuiGuBaHuang-ModLib/3385996759`) is built in its own repository: for a
ModLib error, follow `GuiGuBaHuang-ModLib/.claude/commands/fix-build-errors.md` there and commit the new ModLib DLLs in the submodule.

1. Read `<ProjectId>/scripts/auto-built-errors.log`. If it does not exist, run `<ProjectId>/scripts/rebuild-project.ps1` to get the current errors.
2. If there are compilation errors, fix the source code to resolve them.
3. If there are no compilation errors, review the warnings and suggest source code improvements based on them.
4. After fixing, run `powershell -NoProfile -ExecutionPolicy Bypass -File <ProjectId>/scripts/rebuild-and-deploy.ps1` to verify all errors are resolved.
5. If errors remain, repeat until there are none.
