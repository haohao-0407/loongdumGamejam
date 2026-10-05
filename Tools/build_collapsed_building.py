"""Run inside Blender. Meter-scale, hollow two-storey office ruin for Unity."""
import bpy
import math
import random
import json
import shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path('D:/unity-projects/loongdum')
OUT = ROOT / 'Assets/Environment/CollapsedBuilding'
SOURCE = ROOT / 'SourceArt/CollapsedBuilding'
for p in (OUT / 'Models', OUT / 'Textures', SOURCE, ROOT / 'Captures'):
    p.mkdir(parents=True, exist_ok=True)
random.seed(1947)
scene = bpy.data.scenes.new('CollapsedBuilding_Interior')
bpy.context.window.scene = scene
scene.unit_settings.scale_length = 1.0
groups = {}

def material(name, color, roughness=0.85, metallic=0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, 1)
    bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = roughness
    bs.inputs['Metallic'].default_value = metallic
    return m

concrete = bpy.data.materials.get('concrete_wall_008') or bpy.data.materials.get('Ruin_Concrete')
if concrete is None:
    raise RuntimeError('Import the selected concrete_wall_008 Poly Haven material first.')
concrete.name = 'Ruin_Concrete'
for n in concrete.node_tree.nodes:
    if n.type == 'MAPPING':
        n.inputs['Scale'].default_value = (1, 1, 1)
    if n.type == 'TEX_IMAGE' and n.image:
        src = Path(bpy.path.abspath(n.image.filepath))
        dest = OUT / 'Textures' / src.name
        if src.exists() and src != dest:
            shutil.copy2(src, dest)
        n.image.filepath = str(dest)
plaster = bpy.data.materials.get('Ruin_Plaster') or concrete.copy()
plaster.name = 'Ruin_Plaster'
bs = next(n for n in plaster.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
tile = material('Ruin_Tile', (0.31, 0.32, 0.29))
steel = material('Ruin_RustedSteel', (0.19, 0.095, 0.046), 0.73, 0.72)
dark = material('Ruin_DarkMetal', (0.045, 0.061, 0.061), 0.6, 0.65)
wood = material('Ruin_Wood', (0.24, 0.16, 0.085))
black = material('Ruin_Black', (0.018, 0.025, 0.028), 0.5)
paper = material('Ruin_Paper', (0.53, 0.49, 0.4))
green = material('Ruin_ExitGreen', (0.025, 0.18, 0.09), 0.6)
white = material('Ruin_SignWhite', (0.8, 0.86, 0.71))
emission = material('Ruin_Lamp', (0.6, 0.31, 0.075))
ebs = next(n for n in emission.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
ebs.inputs['Emission Color'].default_value = (1, 0.46, 0.12, 1)
ebs.inputs['Emission Strength'].default_value = 3

def uv_world(obj):
    mesh = obj.data
    mesh.update()
    uv = mesh.uv_layers.new(name='MeterUV')
    for poly in mesh.polygons:
        normal = poly.normal
        axis = max(range(3), key=lambda a: abs(normal[a]))
        pair = [(1, 2), (0, 2), (0, 1)][axis]
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv = (co[pair[0]] / 2.71, co[pair[1]] / 2.71)

def mesh_obj(name, verts, faces, mat, group):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv_world(obj)
    groups.setdefault(group, []).append(obj)
    return obj

def box(name, center, size, mat=concrete, group='COL_Structure', rot=None, bevel=0):
    x,y,z = size
    verts = [(a*x/2,b*y/2,c*z/2) for a,b,c in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    faces = [(0,2,6,4),(1,5,7,3),(0,4,5,1),(2,3,7,6),(0,1,3,2),(4,6,7,5)]
    obj = mesh_obj(name, verts, faces, mat, group)
    obj.location = center
    if rot:
        obj.rotation_euler = tuple(math.radians(v) for v in rot)
    if bevel:
        mod = obj.modifiers.new('Chipped edge bevel','BEVEL')
        mod.width = bevel
        mod.segments = 1
    return obj

def rod(name, a, b, radius=0.018, mat=steel, group='DEC_Rebar', sides=6):
    a,b = Vector(a),Vector(b)
    length = (b-a).length
    verts=[]
    for z in (-length/2,length/2):
        for i in range(sides):
            ang=i*2*math.pi/sides
            verts.append((radius*math.cos(ang),radius*math.sin(ang),z))
    faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
    faces += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    obj=mesh_obj(name,verts,faces,mat,group)
    obj.location=(a+b)/2
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return obj

def slab(name, poly, height, thick=0.28, group='COL_Slabs'):
    n=len(poly)
    verts=[(x,y,height-thick) for x,y in poly]+[(x,y,height) for x,y in poly]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh_obj(name,verts,faces,concrete,group)

def wall_y(name,x,y0,y1,z0,height,doors=(),group='COL_Walls'):
    # Partition parallel to Y, with real 2.5m openings and lintels.
    cursor=y0
    for cy,width in sorted(doors):
        lo,hi=cy-width/2,cy+width/2
        if lo>cursor:
            box(name,(x,(cursor+lo)/2,z0+height/2),(0.24,lo-cursor,height),plaster,group)
        box(name+'_lintel',(x,cy,z0+(height+2.6)/2),(0.24,width,height-2.6),plaster,group)
        cursor=hi
    if cursor<y1:
        box(name,(x,(cursor+y1)/2,z0+height/2),(0.24,y1-cursor,height),plaster,group)

# Ground floor and the upper ring, all volumes are hollow.
box('Foundation',(0,0,-0.25),(36,28,0.5),concrete,'COL_Ground')
for x0,x1,y0,y1 in [(-18,-15.4,-14,14),(-11.6,-8,-14,14),(-15.4,-11.6,-14,3.8),(-15.4,-11.6,12.1,14),(8,18,-14,14),(-8,8,-14,-6.5),(-8,8,7.5,14)]:
    box('Upper_ring',((x0+x1)/2,(y0+y1)/2,4.04),(x1-x0,y1-y0,0.32),concrete,'COL_UpperFloor')
slab('Torn_gallery_edge',[(8,-6.5),(6.6,-6.5),(7,-4.8),(6.35,-3.4),(7.3,-1.8),(6.9,0.2),(7.5,2.1),(6.8,4.3),(7.5,5.8),(6.3,7.5),(8,7.5)],4.2)
slab('Torn_south_edge',[(-8,-6.5),(8,-6.5),(8,-5.7),(5.8,-5.1),(4.2,-6),(2.4,-5.3),(0.8,-5.9),(-1.2,-5.2),(-3.7,-6),(-5.9,-5.4),(-8,-5.85)],4.2)
# Broken roof and partial third-storey silhouette.
for x0,x1,y0,y1 in [(-18,-8,-14,14),(8,18,-14,-7),(-8,8,-14,-7),(-18,-5,10,14)]:
    box('Surviving_roof',((x0+x1)/2,(y0+y1)/2,8.21),(x1-x0,y1-y0,0.38),concrete,'COL_Roof')
slab('Fractured_roof_east',[(8,-7),(18,-7),(18,5),(16.2,5.6),(15.1,4.2),(13.3,5.4),(11.8,4.5),(10.1,5.8),(8,4.8)],8.4,0.38,'COL_Roof')
slab('Fractured_roof_north',[(-5,10),(8,10),(8,14),(-5,14),(-5,13),(-3.9,12),(-4.7,11.2)],8.4,0.38,'COL_Roof')

# Facade panels with empty windows, missing masonry and irregular broken edges.
for side in (-1,1):
    for level in (0,4.2):
        for y in range(-12,14,4):
            if side==1 and y>3 and level>0:
                continue
            box('Facade_sill',(18*side,y,level+0.55),(0.36,3.5,1.1),concrete,'COL_Facade')
            box('Facade_pier',(18*side,y-1.9,level+1.95),(0.44,0.5,3.9),concrete,'COL_Facade')
            if not (side==1 and y in (0,4,8)):
                box('Facade_head',(18*side,y,level+3.7),(0.36,3.5,0.42),concrete,'COL_Facade')
            for yy in (y-1.68,y+1.68):
                box('Window_frame',(17.8*side,yy,level+2.1),(0.06,0.045,1.75),dark,'DEC_WindowFrames')
            box('Window_mullion',(17.8*side,y,level+2.1),(0.06,0.045,1.75),dark,'DEC_WindowFrames')
    for x0,x1 in [(-18,-3.2),(3.2,18)]:
        box('South_wall',((x0+x1)/2,-14,1.9),(x1-x0,0.36,3.8),plaster,'COL_Facade')
    box('Entrance_lintel',(0,-14,3.45),(6.4,0.4,0.7),concrete,'COL_Facade')
    break
for level in (4.2,):
    for x in (-15,-10,-5,0,5,10,15):
        box('South_upper_sill',(x,-14,level+0.6),(4.5,0.38,1.2),concrete,'COL_Facade')
        box('South_upper_pier',(x-2.35,-14,level+2),(0.42,0.42,4),concrete,'COL_Facade')
        box('South_upper_head',(x,-14,level+3.8),(4.7,0.42,0.4),concrete,'COL_Facade')
for x0,x1,h in [(-18,-10,8.5),(-10,-2,6.8),(-2,5,4.8),(5,11,3.1),(11,18,1.2)]:
    # Broken skyline on north wall.
    p=[(x0,0),(x1,0),(x1,h-0.55),(x1-0.7,h-0.35),(x1-1.2,h),(x1-2,h-0.4),(x0+0.8,h+0.15),(x0,h-0.2)]
    verts=[(x,14+t,z) for t in (-0.17,0.17) for x,z in p]
    n=len(p)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh_obj('Broken_north_wall',verts,faces,concrete,'COL_Facade')

# Columns and transfer beams show the original office grid.
for x in (-8,8):
    for y in (-10,-4,3,10):
        h=8.2 if not(x==8 and y==3) else 3.0
        box('Column',(x,y,h/2),(0.68,0.68,h),concrete,'COL_Columns',bevel=0.045)
        box('Column_base',(x,y,0.15),(0.95,0.95,0.3),concrete,'COL_Columns')
    for z in (4.0,8.05):
        box('Longitudinal_beam',(x,0,z),(0.5,27.5,0.48),concrete,'COL_Beams')
for y in (-10,10):
    for z in (4.0,8.05):
        if y==10 and z==4.0:
            # Leave structural headroom over the stair opening.
            for x0,x1 in [(-17.75,-15.4),(-11.6,17.75)]:
                box('Transfer_beam',((x0+x1)/2,y,z),(x1-x0,0.55,0.48),concrete,'COL_Beams')
        else:
            box('Transfer_beam',(0,y,z),(35.5,0.55,0.48),concrete,'COL_Beams')
for x in (-8,8):
    wall_y('Office_partition',x,-13.6,13.6,0,3.9,[(-10,2.6),(-0.5,2.8),(6,2.8)],'COL_LowerPartitions')
    wall_y('Upper_partition',x,-13.6,13.6,4.2,3.8,[(-10,2.6),(0,2.8),(9,2.8)],'COL_UpperPartitions')
for x in (-13,13):
    for y in (-5.5,3):
        if x<0 and y==3:
            continue
        box('Room_divider',(x,y,1.85),(9.6,0.2,3.7),plaster,'COL_LowerPartitions')
    box('Office_skirt',(x,-13.75,0.1),(9.5,0.08,0.2),dark,'DEC_Trim')

# Real stairs through a matching opening in the upper slab.
for i in range(24):
    top=(i+1)*4.2/24
    box('Stair_tread',(-13.5,4.05+(i+0.5)*8/24,top/2),(3.1,8/24+0.025,top),concrete,'COL_Stairs')
for x in (-15.15,-11.85):
    rod('Stair_handrail',(x,4.1,1.02),(x,12.05,5.18),0.035,dark,'DEC_Rails')
    for i in range(0,24,4):
        y=4.05+(i+0.5)*8/24
        z=(i+1)*4.2/24
        rod('Stair_baluster',(x,y,z),(x,y,z+1),0.022,dark,'DEC_Rails')
# Ring balustrades. Deliberate missing section at the collapse.
for x in (-7.75,7.75):
    for y0,y1 in [(-6.3,-1.5),(2.5,7.5)]:
        rod('Gallery_rail',(x,y0,5.3),(x,y1,5.3),0.035,dark,'COL_Guardrails')
        rod('Gallery_midrail',(x,y0,4.75),(x,y1,4.75),0.024,dark,'COL_Guardrails')
        for i in range(math.ceil(y1-y0)+1):
            yy=min(y1,y0+i)
            rod('Gallery_post',(x,yy,4.2),(x,yy,5.3),0.03,dark,'COL_Guardrails')
for y in (-6.7,7.7):
    rod('Bridge_rail',(-7.7,y,5.3),(7.7,y,5.3),0.036,dark,'COL_Guardrails')
    rod('Bridge_midrail',(-7.7,y,4.75),(7.7,y,4.75),0.025,dark,'COL_Guardrails')
    for x in range(-7,8):
        rod('Bridge_post',(x,y,4.2),(x,y,5.3),0.025,dark,'COL_Guardrails')

# Large displaced slabs sit beside, never across, the 3m central route.
for idx,(center,size,rot) in enumerate([((4.3,3.6,1.0),(5.0,4.0,0.34),(19,-24,14)),((5.4,6.5,1.6),(3.7,4.3,0.3),(-37,13,23)),((11.5,9.6,1.5),(5.2,3.9,0.36),(31,-17,-12)),((2.5,5.8,0.45),(3.2,2.8,0.28),(8,9,27)),((14.2,6.2,4.9),(4.3,3.7,0.3),(23,-34,-10))]):
    obj=box('Fallen_slab_'+str(idx),center,size,concrete,'COL_Collapse',rot)
    # Fracture the perimeter so slabs do not look like clean rectangles.
    for v in obj.data.vertices:
        v.co.x+=random.uniform(-0.22,0.22)
        v.co.y+=random.uniform(-0.17,0.17)
    for j in range(8):
        start=(center[0]+random.uniform(-size[0]/2,size[0]/2),center[1]+random.uniform(-size[1]/2,size[1]/2),center[2]+0.15)
        end=(start[0]+random.uniform(-0.6,0.6),start[1]+random.uniform(0.5,1.4),start[2]+random.uniform(0.1,0.5))
        rod('Exposed_rebar',start,end)
        rod('Bent_rebar_tip',end,(end[0]+0.3,end[1]+0.2,end[2]-0.25))

def rubble(cx,cy,count,spread,zbase=0):
    for i in range(count):
        x=random.gauss(cx,spread)
        y=random.gauss(cy,spread)
        if abs(x)<1.8 and y<8.5:
            continue
        if not(-17.5<x<17.5 and -13.5<y<13.5):
            continue
        size=random.uniform(0.12,0.6)
        if i<12:
            size=random.uniform(0.5,1.2)
        h=size*random.uniform(0.3,0.8)
        poly=[(math.cos(a*math.pi/3)*size*random.uniform(0.5,1),math.sin(a*math.pi/3)*size*random.uniform(0.5,1)) for a in range(6)]
        ob=slab('Concrete_shard',poly,h, h,'DEC_Rubble')
        ob.location=(x,y,zbase)
        ob.rotation_euler.z=random.random()*math.pi*2
        for v in ob.data.vertices:
            if v.co.z>0:
                v.co.z*=random.uniform(0.65,1.15)
    for i in range(count//6):
        x=random.gauss(cx,spread)
        y=random.gauss(cy,spread)
        if abs(x)<1.8 or not(-17<x<17 and -13<y<13):
            continue
        box('Brick',(x,y,zbase+0.07),(0.22,0.11,0.13),wood,'DEC_Bricks',rot=(0,random.uniform(-10,10),random.uniform(0,180)))
for cx,cy,cnt,spr,base in [(5,5,150,2,0),(13,9,120,2.1,0),(-5,-3,65,1.2,0),(-16,-10,45,1.2,0),(11,2,60,1.3,4.2),(4,-9,30,1,4.2)]:
    rubble(cx,cy,cnt,spr,base)

# Lobby tile joints, directional wayfinding and the reception desk.
for x in range(-7,8):
    box('Tile_joint',(x,-0.7,0.003),(0.014,25,0.006),dark,'DEC_TileJoints')
for y in range(-13,13):
    box('Tile_joint',(0,y,0.003),(15.5,0.014,0.006),dark,'DEC_TileJoints')
box('Reception_front',(-4.4,-7.6,0.65),(4,0.85,1.3),wood,'COL_Furniture',bevel=0.04)
box('Reception_worktop',(-4.4,-7.6,1.34),(4.15,1,0.12),dark,'COL_Furniture')
for x,y in [(-15,-10),(-11,-8),(-16,-1),(-11,0),(12,-10),(16,-9),(11,0),(16,1),(-16,-10),(12,-10)]:
    z=4.2 if(x,y) in [(-16,-10),(12,-10)] else 0
    box('Desk_top',(x,y,z+0.77),(1.6,0.8,0.075),wood,'COL_Furniture',rot=(0,0,random.uniform(-9,9)))
    for dx in (-0.65,0.65):
        box('Desk_leg',(x+dx,y,z+0.36),(0.08,0.65,0.72),dark,'COL_Furniture')
    box('Monitor',(x,y+0.2,z+1.1),(0.55,0.075,0.38),black,'DEC_Office')
    box('Monitor_stand',(x,y+0.2,z+0.86),(0.12,0.1,0.18),dark,'DEC_Office')
    box('Keyboard',(x,y-0.2,z+0.83),(0.45,0.18,0.025),black,'DEC_Office')
for x,y,z in [(-15,-2,0),(15,10,0),(-15,-3,4.2)]:
    box('Filing_cabinet',(x,y,z+0.65),(0.9,0.6,1.3),dark,'COL_Furniture',rot=(0,0,12))
    for h in (0.24,0.62,1.02):
        box('Drawer_face',(x,y-0.31,z+h),(0.83,0.035,0.32),steel,'DEC_Office')
for i in range(38):
    x,y=random.uniform(-16,16),random.uniform(-12,11)
    box('Loose_paper',(x,y,0.015),(0.21,0.3,0.008),paper,'DEC_Office',rot=(0,0,random.uniform(0,180)))

# Splayed ceiling grids, ripped ducts and hanging services.
for x,y,z,length,rot in [(-3,-4,3.35,5,(12,0,17)),(3,1,5.6,6,(0,24,-13)),(10,5,6.5,4,(25,0,4))]:
    box('Broken_duct',(x,y,z),(0.72,length,0.52),dark,'COL_Ducts',rot=rot)
    for j in range(8):
        rod('Duct_rib',(x-0.38,y-length/2+j*length/8,z-0.27),(x+0.38,y-length/2+j*length/8,z-0.27),0.018,steel,'DEC_Services')
for x in (-5,1,6):
    for y in (-10,-7):
        box('Ceiling_frame',(x,y,3.6),(2.7,0.04,0.05),dark,'DEC_Services',rot=(random.uniform(-12,12),0,random.uniform(-8,8)))
for i in range(13):
    x,y=random.uniform(2,8),random.uniform(-5,8)
    last=(x,y,7.9)
    for j in range(7):
        pt=(x+math.sin(j*0.6)*0.12,y+math.sin(j*0.8)*0.2,7.9-j*0.38)
        if j:
            rod('Hanging_cable',last,pt,0.012,black,'DEC_Services',5)
        last=pt

# Small sign meshes are baked into FBX, no fonts needed at runtime.
def sign_text(body,loc,size,mat,rotation=(90,0,0)):
    curve=bpy.data.curves.new('Wayfinding','FONT')
    curve.body=body
    curve.size=size
    curve.extrude=0.001
    obj=bpy.data.objects.new('Sign_'+body,curve)
    scene.collection.objects.link(obj)
    obj.location=loc
    obj.rotation_euler=tuple(math.radians(v) for v in rotation)
    curve.materials.append(mat)
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj.select_set(False)
    groups.setdefault('DEC_Signs',[]).append(obj)

box('Lobby_sign',(-4,-13.78,2.8),(5,0.06,0.72),dark,'DEC_Signs')
sign_text('NORTH TOWER',(-1.85,-13.72,2.65),0.4,white,(90,0,180))
box('Exit_sign',(-8.16,6,3.05),(0.05,1.2,0.35),green,'DEC_Signs')
sign_text('STAIR 01',(-8.18,5.55,2.95),0.15,white,(90,0,90))
sign_text('01',(-7.61,-9.75,1.8),0.55,paper,(90,0,90))
sign_text('02',(-7.61,-9.75,6),0.55,paper,(90,0,90))

# Combine spatial/category batches to keep game draw calls manageable.
for x in (-17,-9):
    for y in (9,13):
        h=4.4 if (x,y)!=(-9,9) else 2.8
        box('Upper_broken_column',(x,y,8.4+h/2),(.55,.55,h),concrete,'COL_UpperRuins',rot=(0,3 if y==9 else -2,0))
for name,center,size,rot in [
    ('Third_floor_remnant',(-13,11,12.45),(10,5.5,.32),(2,5,0)),
    ('Fourth_floor_fold',(-13,11.8,14.6),(8.5,4.6,.34),(9,18,-6)),
    ('Collapsed_roof_stack',(12,8.8,10.7),(9,6,.35),(-18,21,8)),
    ('Leaning_transfer_beam',(13.5,9,9.5),(.55,.55,6),(16,35,11)),
    ('Broken_skyline_panel',(-17.4,12,10.5),(.3,4,3.8),(0,-8,0))]:
    box(name,center,size,concrete,'COL_UpperRuins',rot=rot)
bpy.ops.object.select_all(action='DESELECT')
for name,objects in groups.items():
    existing=[o for o in objects if o.name in scene.objects]
    if not existing:
        continue
    for o in existing:
        o.select_set(True)
        bpy.context.view_layer.objects.active=o
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active=existing[0]
    bpy.ops.object.join()
    obj=existing[0]
    obj.name=name
    # Bake transform to world-space mesh. FBX preserves origin and meter scale.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.select_set(False)

# Blender inspection cameras; export only meshes to Unity.
def camera(name,loc,target,lens=23):
    data=bpy.data.cameras.new(name)
    data.lens=lens
    data.clip_end=200
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=loc
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    return obj
scene.camera=camera('Interior_Hero',(-2.8,-11.4,1.7),(3,3,3.3),20)
camera('Overview',(35,-39,29),(0,0,3.5),38)
world=bpy.data.worlds.new('Ruin_Daylight')
world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(0.48,0.58,0.7,1)
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=0.4
scene.world=world
for name,kind,loc,power,color,size,target in [('Sun','SUN',(9,3,18),2.2,(1,0.86,0.66),0,(0,0,0)),('Skylight','AREA',(1,2,12),3200,(0.67,0.79,1),14,(0,0,0)),('Entrance_fill','AREA',(0,-12,3),350,(0.83,0.88,1),5,(0,0,2))]:
    d=bpy.data.lights.new(name,kind)
    d.energy=power
    d.color=color
    if kind=='AREA':
        d.size=size
    ob=bpy.data.objects.new(name,d)
    scene.collection.objects.link(ob)
    ob.location=loc
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()

scene.render.resolution_x=1440
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG' # Verified against live enum before authoring.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
scene['design']='36 x 28m hollow two-storey ruin; 4.2m storey height; 3m main route; 2.6m+ doors; 24-step west staircase; open roof and atrium.'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CollapsedBuilding.blend'))
for obj in scene.objects:
    obj.select_set(obj.type=='MESH')
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/CollapsedBuilding.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_anim=False,use_triangles=True,path_mode='RELATIVE')
manifest={'dimensions_m':[36,28,8.4],'storey_height_m':4.2,'main_clear_width_m':3.0,'source':'SourceArt/CollapsedBuilding/CollapsedBuilding.blend','texture':{'id':'concrete_wall_008','source':'https://polyhaven.com/a/concrete_wall_008','license':'CC0','authors':'Charlotte Baglioni, Dario Barresi'},'meshes':[]}
for obj in scene.objects:
    if obj.type=='MESH':
        manifest['meshes'].append({'name':obj.name,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons)})
(SOURCE/'geometry_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'saved':str(SOURCE/'CollapsedBuilding.blend'),'fbx':str(OUT/'Models/CollapsedBuilding.fbx'),'mesh_batches':len(manifest['meshes']),'triangles':sum(m['triangles'] for m in manifest['meshes'])}))
