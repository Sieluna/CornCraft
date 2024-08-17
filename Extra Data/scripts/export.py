# A Python3 script used for exporting protocol id mappings from
# registries.json generated by https://wiki.vg/Data_Generators
import json

mc_ver = '16'

with open(f'D:/Minecraft/Servers/MC{mc_ver}Pure/generated/reports/registries.json') as f:
    lines = f.read()

    itemList = { }
    
    dict = json.loads(lines)
    entries = dict['minecraft:entity_type']['entries']

    with open(f'Extra Data/entity_types_code-1.{mc_ver}.txt', 'w+') as out:
        for key, value in entries.items():
            numId = int(value['protocol_id'])
            itemList[numId] = key
            print(f'[{numId}] {key}')
            name = key.split(":")[1]
            code = f'public static readonly ResourceLocation {name.upper()}_ID = new("{name}");\n'
            out.write(code)

    with open(f'Extra Data/entity_types-1.{mc_ver}.json', 'w+') as out:
        text = json.dumps(itemList, indent=4, separators=(',', ': '))
        out.write(text)