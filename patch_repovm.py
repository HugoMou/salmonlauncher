import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

old1 = '''public string SearchHint => _category switch
        {
            ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
            ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
            _ => Loc.T("搜索资源包(如 faithful、stay true)"),
        };'''

new1 = '''public string SearchHint => _category switch
        {
            ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
            ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
            ModrinthCategory.ResourcePacks => Loc.T("搜索资源包(如 faithful、stay true)"),
            ModrinthCategory.Modpacks => Loc.T("搜索整合包(如 ATM、RLCraft、Create)"),
        };'''

old2 = '''public string SubDir => _category switch
        {
            ModrinthCategory.Mods => "mods",
            ModrinthCategory.Shaders => "shaderpacks",
            _ => "resourcepacks",
        };'''

new2 = '''public string SubDir => _category switch
        {
            ModrinthCategory.Mods => "mods",
            ModrinthCategory.Shaders => "shaderpacks",
            ModrinthCategory.ResourcePacks => "resourcepacks",
            ModrinthCategory.Modpacks => "modpacks",
        };'''

text = text.replace(old1, new1, 1)
text = text.replace(old2, new2, 1)
p.write_text(text, encoding='utf-8')
print('Done SearchHint and SubDir')