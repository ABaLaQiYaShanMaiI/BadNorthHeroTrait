import os

base = r'f:\BadNorthProgram\BadNorthHeroTraits'

projects = [
    r'BadNorthAxeThrower\BadNorthAxeThrower\BadNorthAxeThrower.csproj',
    r'BadNorthCheaperClass\BadNorthCheaperClass\BadNorthCheaperClass.csproj',
    r'BadNorthRegenerative\BadNorthRegenerative\BadNorthRegenerative.csproj',
    r'BadNorthThorns\BadNorthThorns\BadNorthThorns.csproj',
]

for proj in projects:
    path = os.path.join(base, proj)
    if os.path.exists(path):
        content = open(path, 'r', encoding='utf-8').read()
        content = content.replace('v3.5', 'v4.8')
        open(path, 'w', encoding='utf-8').write(content)
        print(f'Fixed: {proj}')
    else:
        print(f'Not found: {proj}')
