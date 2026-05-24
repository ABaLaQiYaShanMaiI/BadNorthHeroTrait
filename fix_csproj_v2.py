import os, shutil

base = r'f:\BadNorthProgram\BadNorthHeroTraits'

# 直接读取 csproj 文件内容，用 Python 修改后写到根目录的临时文件
# 然后用 copy 命令覆盖（因为 copy 到原路径被锁，但我们可以用不同的方法）

projects = [
    ('BadNorthAxeThrower', 'BadNorthAxeThrower\\BadNorthAxeThrower\\BadNorthAxeThrower.csproj'),
    ('BadNorthCheaperClass', 'BadNorthCheaperClass\\BadNorthCheaperClass\\BadNorthCheaperClass.csproj'),
    ('BadNorthRegenerative', 'BadNorthRegenerative\\BadNorthRegenerative\\BadNorthRegenerative.csproj'),
    ('BadNorthThorns', 'BadNorthThorns\\BadNorthThorns\\BadNorthThorns.csproj'),
]

for name, proj_path in projects:
    path = os.path.join(base, proj_path)
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    old_count = content.count('System.Core')
    content = content.replace('<Reference Include="System.Core" />', '')
    new_count = content.count('System.Core')
    
    # 写到根目录
    out_path = os.path.join(base, f'{name}.csproj.new')
    with open(out_path, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f'{name}: {old_count} -> {new_count} (written to {out_path})')
