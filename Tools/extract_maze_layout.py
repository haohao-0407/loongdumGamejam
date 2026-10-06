"""Read the saved Maze hierarchy without changing the source scene."""
import json, re, hashlib
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
path = ROOT / 'Assets/Scenes/Maze.unity'
source = path.read_text(encoding='utf-8')
objects, transforms = {}, {}
def vector(body, name):
    line = re.search(r'  '+name+r': \{([^}]+)\}', body).group(1)
    return [float(v) for v in re.findall(r'[xyzw]: ([^,}]+)', line)]
for cls, ident, body in re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=--- !u!|\Z)', source, re.S):
    if cls == '1':
        objects[int(ident)] = re.search(r'  m_Name: (.*)', body).group(1).strip()
    elif cls == '4':
        transforms[int(ident)] = dict(go=int(re.search(r'm_GameObject: \{fileID: (\d+)',body).group(1)),
            parent=int(re.search(r'm_Father: \{fileID: (\d+)',body).group(1)),
            position=vector(body,'m_LocalPosition'), scale=vector(body,'m_LocalScale'),rotation=vector(body,'m_LocalRotation'))
root_id, root = next((i,t) for i,t in transforms.items() if objects.get(t['go'])=='GeneratedMaze')
children = [dict(name=objects[t['go']], **t) for t in transforms.values() if t['parent']==root_id]
base = next(t for t in children if t['name']=='PlatformBase')
origin = [base['position'][0], base['position'][1]+base['scale'][1]/2, base['position'][2]]
walls=[]
for t in children:
    p=[(t['position'][i]-origin[i])*root['scale'][i] for i in range(3)]
    s=[t['scale'][i]*root['scale'][i] for i in range(3)]
    walls.append(dict(name=t['name'], position=p, size=s))
data=dict(source='Assets/Scenes/Maze.unity',sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
    root=root, localOrigin=origin, cellSize=[root['scale'][0],root['scale'][2]],objects=walls,
    note='Metre coordinates aligned with GeneratedMaze rotation. Unity XYZ; Blender XZY.')
out=ROOT/'SourceArt/MazeAftermath/maze_layout.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(data,indent=2),encoding='utf-8')
print(json.dumps(data,indent=2))
