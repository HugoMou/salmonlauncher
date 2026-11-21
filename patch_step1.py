import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# ============================================================
# 1. SearchHint - add Modpacks case
# ============================================================
old1 = '''    public string SearchHint => _category switch
    {
        ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
        ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
        _ => Loc.T("搜索资源包(如 faithful、stay true)"),
    };'''

new1 = '''    public string SearchHint => _category switch
    {
        ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
        ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
        ModrinthCategory.ResourcePacks => Loc.T("搜索资源包(如 faithful、stay true)"),
        ModrinthCategory.Modpacks => Loc.T("搜索整合包(如 ATM、RLCraft、Create)"),
    };'''

# ============================================================
# 2. SubDir
# ============================================================
old2 = '''    public string SubDir => _category switch
    {
        ModrinthCategory.Mods => "mods",
        ModrinthCategory.Shaders => "shaderpacks",
        _ => "resourcepacks",
    };'''

new2 = '''    public string SubDir => _category switch
    {
        ModrinthCategory.Mods => "mods",
        ModrinthCategory.Shaders => "shaderpacks",
        ModrinthCategory.ResourcePacks => "resourcepacks",
        ModrinthCategory.Modpacks => "modpacks",
    };'''

# ============================================================
# 3. Configure - Title switch
# ============================================================
old3 = '''        Title = _category switch
        {
            ModrinthCategory.Mods => Loc.T("模组仓库"),
            ModrinthCategory.Shaders => Loc.T("光影仓库"),
            _ => Loc.T("资源包仓库"),
        };'''

new3 = '''        Title = _category switch
        {
            ModrinthCategory.Mods => Loc.T("模组仓库"),
            ModrinthCategory.Shaders => Loc.T("光影仓库"),
            ModrinthCategory.ResourcePacks => Loc.T("资源包仓库"),
            ModrinthCategory.Modpacks => Loc.T("整合包仓库"),
        };'''

# Apply patches
text = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').read_text(encoding='utf-8')

replacements = [
    ("SearchHint", old1, new1),
    ("SubDir", old2, new2),
    ("Configure Title", old3, new3),
]

for name, old, new in replacements:
    if old in text:
        text = text.replace(old, new, 1)
        print(f"✓ {name} patched")
    else:
        print(f"✗ {name} NOT FOUND")

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text, encoding='utf-8')
print("Step 1 done")