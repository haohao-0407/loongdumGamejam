"""Final battle-scarred wall surface, replacing the temporary geometric paint/impact overlay."""
import bpy,json,struct
from pathlib import Path
ROOT=Path('D:/unity-projects/loongdum');OUT=ROOT/'Assets/Environment/MazeAftermath'
scene=bpy.data.scenes['Maze_Aftermath'];bpy.context.window.scene=scene
material=bpy.data.materials['Aftermath_DamagedConcrete']
bs=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
tex=next(n for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'Diffuse' in n.image.name)
tex.image=bpy.data.images.load(str(OUT/'Textures/SpalledWall_BaseColor.png'),check_existing=True)
# Keep the previous paint/mark overlay in the editable source, hidden as an alternate style.
patina=bpy.data.objects['Aftermath_Patina'];patina.hide_render=True;patina.hide_set(True)
asphalt=bpy.data.materials['Aftermath_Asphalt'];absdf=next(n for n in asphalt.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
roadtex=asphalt.node_tree.nodes.new('ShaderNodeTexImage');roadtex.image=bpy.data.images.load(str(OUT/'Textures/BattleConcrete_BaseColor.png'),check_existing=True)
mix=asphalt.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.24,.26,.28,1)
asphalt.node_tree.links.new(roadtex.outputs['Color'],mix.inputs[1]);asphalt.node_tree.links.new(mix.outputs['Color'],absdf.inputs['Base Color'])
for o in bpy.data.objects:o.select_set(False)
for o in scene.objects:
    if o.type=='MESH' and o.name.startswith('Aftermath_') and o.name!='Aftermath_Patina':o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'Models/MazeAftermath_Architecture.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_yup=True,export_cameras=False,export_lights=False)
# Blender's glTF exporter omits the MixRGB constant multiplier. Persist its PBR factor explicitly.
glbpath=OUT/'Models/MazeAftermath_Architecture.glb';raw=glbpath.read_bytes();length=struct.unpack_from('<I',raw,12)[0]
document=json.loads(raw[20:20+length]);rest=raw[20+length:]
for entry in document['materials']:
    if entry['name']=='Aftermath_Asphalt':entry.setdefault('pbrMetallicRoughness',{})['baseColorFactor']=[.24,.26,.28,1]
encoded=json.dumps(document,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
glbpath.write_bytes(struct.pack('<III',0x46546c67,2,20+len(encoded)+len(rest))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+rest)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/MazeAftermath/MazeAftermath.blend'))
print('Polished wall texture, archived alternate overlay, exported 11 architecture batches')
