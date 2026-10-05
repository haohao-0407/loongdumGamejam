"""Run in the saved Blender scene after Meshy FBX and texture extraction finish."""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/unity-projects/loongdum')
scene=bpy.context.scene
if bpy.data.collections.get('Meshy_SetDressing'):
    raise RuntimeError('Meshy_SetDressing already exists; do not import duplicate props.')
collection=bpy.data.collections.new('Meshy_SetDressing')
scene.collection.children.link(collection)

def import_prop(name,places,size):
    before=set(scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Environment/CollapsedBuilding/Meshy'/f'{name}.fbx'))
    objects=[o for o in scene.objects if o not in before and o.type=='MESH']
    if not objects:
        raise RuntimeError('No mesh imported for '+name)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:
        bpy.ops.object.join()
    ob=objects[0]
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bounds=[Vector(v) for v in ob.bound_box]
    lo=Vector([min(v[a] for v in bounds) for a in range(3)])
    hi=Vector([max(v[a] for v in bounds) for a in range(3)])
    scale=size/max(hi-lo)
    center=(hi+lo)/2
    for vertex in ob.data.vertices:
        vertex.co=(vertex.co-Vector((center.x,center.y,lo.z)))*scale
    mat=bpy.data.materials.new('Meshy_'+name)
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    texdir=ROOT/'Assets/Environment/CollapsedBuilding/Meshy/Textures'/name
    for filename,input_name in [('Image_0.jpg','Base Color'),('texture_0_metallic.png','Metallic'),('texture_0_roughness.png','Roughness')]:
        node=nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(texdir/filename),check_existing=True)
        if input_name!='Base Color':
            node.image.colorspace_settings.name='Non-Color'
        mat.node_tree.links.new(node.outputs['Color'],bs.inputs[input_name])
    node=nodes.new('ShaderNodeTexImage')
    node.image=bpy.data.images.load(str(texdir/'Image_2.jpg'),check_existing=True)
    node.image.colorspace_settings.name='Non-Color'
    normal=nodes.new('ShaderNodeNormalMap')
    mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color'])
    mat.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    for old_collection in list(ob.users_collection):
        old_collection.objects.unlink(ob)
    collection.objects.link(ob)
    for idx,place in enumerate(places):
        inst=ob if idx==0 else ob.copy()
        if idx:
            collection.objects.link(inst)
        inst.name=name+'_'+str(idx+1)
        inst.location=(place[0],place[2],place[1])
        inst.rotation_euler=(0,0,math.radians((idx+1)*71))
        inst['source']='Meshy 6 / generated for this project'

import_prop('RebarConcreteDebris',[(5.8,0,2.2),(12.2,0,8),(-5.5,0,-2),(12,4.2,2)],3.4)
import_prop('DamagedOfficeDesk',[(12,0,-7),(-14,0,-1),(15,4.2,-9)],1.8)
scene.camera=bpy.data.objects['Interior_Hero']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/CollapsedBuilding/CollapsedBuilding.blend'))
print('Saved complete Blender scene including 7 textured Meshy instances')
