import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Fix SearchHint
old_search = '''public string SearchHint => _category switch
        {
            ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
            ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
            _ => Loc.T("搜索资源包(如 faithful、stay true)"),
        };'''

new_search = '''public string SearchHint => _category switch
        {
            ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
            ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
            ModrinthCategory.ResourcePacks => Loc.T("搜索资源包(如 faithful、stay true)"),
            ModrinthCategory.Modpacks => Loc.T("搜索整合包(如 ATM、RLCraft、Create)"),
        };'''

# Fix SubDir
old_subdir = '''public string SubDir => _category switch
        {
            ModrinthCategory.Mods => "mods",
            ModrinthCategory.Shaders => "shaderpacks",
            _ => "resourcepacks",
        };'''

new_subdir = '''public string SubDir => _category switch
        {
            ModrinthCategory.Mods => "mods",
            ModrinthCategory.Shaders => "shaderpacks",
            ModrinthCategory.ResourcePacks => "resourcepacks",
            ModrinthCategory.Modpacks => "modpacks",
        };'''

text = p.read_text(encoding='utf-8')
text = text.replace(old_search, new_search, 1)
text = text.replace(old_subdir, new_subdir, 1)
p.write_text(text, encoding='utf-8')
print('Fixed SearchHint and SubDir')