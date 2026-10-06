"""Finish material UVs, import T2 assets, and save the complete editable Blender scene."""
import bpy, json, math
from mathutils import Vector
from pathlib import Path
ROOT=Path('D:/unity-projects/loongdum'); OUT=ROOT/'Assets/Environment/MazeAftermath'; SOURCE=ROOT/'SourceArt/MazeAftermath'
scene=bpy.data.scenes['Maze_Aftermath']; bpy.context.window.scene=scene
manifest=json.loads((SOURCE/'manifest.json').read_text())
for o in scene.objects:
    if o.type!='MESH': continue
    uv=o.data.uv_layers.active
    for poly in o.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a])); pair=[(1,2),(0,2),(0,1)][axis]
        for li in poly.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=(co[pair[0]]/2.71,co[pair[1]]/2.71)
floor=bpy.data.materials.new('Aftermath_TexturedPaving'); floor.use_nodes=True
bs=next(n for n in floor.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); bs.inputs['Roughness'].default_value=.93
tex=floor.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(OUT/'Textures/BattleConcrete_BaseColor.png'),check_existing=True)
floor.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
o=bpy.data.objects['Aftermath_Paving']; o.data.materials.clear(); o.data.materials.append(floor)
for p in o.data.polygons: p.material_index=0
for o in bpy.data.objects: o.select_set(False)
for o in scene.objects: o.select_set(o.type=='MESH')
bpy.ops.export_scene.gltf(filepath=str(OUT/'Models/MazeAftermath_Architecture.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_yup=True,export_cameras=False,export_lights=False)

if 'Meshy_T2_SetDressing' in bpy.data.collections: raise RuntimeError('T2 collection exists; avoid duplicate import')
collection=bpy.data.collections.new('Meshy_T2_SetDressing'); scene.collection.children.link(collection)
templates={}
for name in ('BurnedJeep_T2','RebarRubble_T2'):
    before=set(scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT/'Meshy'/f'{name}.glb'))
    added=[o for o in scene.objects if o not in before]; meshes=[o for o in added if o.type=='MESH']
    for o in meshes:
        matrix=o.matrix_world.copy(); o.parent=None; o.matrix_world=matrix
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    if len(meshes)>1: bpy.ops.object.join()
    obj=meshes[0]; bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    lo=Vector([min(v.co[a] for v in obj.data.vertices) for a in range(3)])
    hi=Vector([max(v.co[a] for v in obj.data.vertices) for a in range(3)])
    factor=1/max(hi.x-lo.x,hi.y-lo.y)
    for v in obj.data.vertices: v.co=(v.co-Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z)))*factor
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj); templates[name]=obj
    for o in added:
        if o.name in bpy.data.objects and o.type!='MESH': bpy.data.objects.remove(o,do_unlink=True)
counts={k:0 for k in templates}
for p in manifest['props']:
    name=p['asset']; src=templates[name]
    obj=src if counts[name]==0 else src.copy()
    if counts[name]: collection.objects.link(obj)
    counts[name]+=1; obj.name=name+'_'+str(counts[name]).zfill(2)
    obj.location=(p['x'],p['y'],.018); obj.scale=(p['size'],)*3
    obj.rotation_euler=(0,0,math.radians(p['angle']))
    obj['source_model']='meshy-t2'; obj['reference_image']=str(OUT/'Reference'/('BurnedJeep.png' if name=='BurnedJeep_T2' else 'RebarRubble.png'))
scene.camera=bpy.data.objects['Aftermath_Overview']
# Pack generated model textures and the local surface maps for a portable .blend source.
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MazeAftermath.blend'))
print(json.dumps({'scene':scene.name,'mesh_objects':sum(o.type=='MESH' for o in scene.objects),'t2_instances':counts,'source':str(SOURCE/'MazeAftermath.blend')}))
