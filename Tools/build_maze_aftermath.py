"""Run in Blender 5.2: dress the measured Maze layout at metre scale."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/unity-projects/loongdum')
OUT=ROOT/'Assets/Environment/MazeAftermath'
SOURCE=ROOT/'SourceArt/MazeAftermath'
layout=json.loads((SOURCE/'maze_layout.json').read_text())
random.seed(61026)
scene=bpy.data.scenes.new('Maze_Aftermath')
bpy.context.window.scene=scene
scene.unit_settings.scale_length=1
groups={}
for p in (OUT/'Models',SOURCE,ROOT/'Captures/MazeAftermath'): p.mkdir(parents=True,exist_ok=True)

def mat(name,color,rough=.9,metal=0,texture=False,emission=0):
    m=bpy.data.materials.new('Aftermath_'+name); m.use_nodes=True
    m.diffuse_color=(*color,1)
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=rough
    bs.inputs['Metallic'].default_value=metal
    if texture:
        node=m.node_tree.nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(ROOT/'Assets/Environment/CollapsedBuilding/Textures/concrete_wall_008_Diffuse_2k.jpg'),check_existing=True)
        m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
        node=m.node_tree.nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(ROOT/'Assets/Environment/CollapsedBuilding/Textures/concrete_wall_008_nor_gl_2k.jpg'),check_existing=True)
        node.image.colorspace_settings.name='Non-Color'
        normal=m.node_tree.nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value=.75
        m.node_tree.links.new(node.outputs['Color'],normal.inputs['Color'])
        m.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
    if emission:
        bs.inputs['Emission Color'].default_value=(*color,1)
        bs.inputs['Emission Strength'].default_value=emission
    return m

concrete=mat('DamagedConcrete',(.52,.50,.44),texture=True)
aggregate=mat('ExposedAggregate',(.30,.275,.235))
plaster=mat('FadedTealPaint',(.12,.21,.20))
brick=mat('OldBrick',(.25,.115,.070))
steel=mat('RustSteel',(.13,.068,.034),.8,.65)
charcoal=mat('Charcoal',(.023,.026,.024))
asphalt=mat('Asphalt',(.11,.115,.11))
dust=mat('Dust',(.33,.30,.255))
yellow=mat('FadedOchre',(.58,.37,.07))
ember=mat('Ember',(.8,.12,.012),emission=3)
tiles=[mat('Tile_%s'%i,(.25+i*.027,.255+i*.025,.238+i*.02)) for i in range(5)]

def mesh(name,verts,faces,material,group='Details'):
    d=bpy.data.meshes.new(name); d.from_pydata(verts,[],faces); d.update()
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o)
    d.materials.append(material)
    uv=d.uv_layers.new(name='WorldMetres')
    for poly in d.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a])); pair=[(1,2),(0,2),(0,1)][axis]
        for li in poly.loop_indices:
            co=d.vertices[d.loops[li].vertex_index].co
            uv.data[li].uv=(co[pair[0]]/2.71,co[pair[1]]/2.71)
    groups.setdefault(group,[]).append(o)
    return o

def box(name,p,s,m=concrete,group='Details',angle=0,bevel=0):
    verts=[(a*s[0]/2,b*s[1]/2,c*s[2]/2) for a,b,c in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    faces=[(0,2,6,4),(1,5,7,3),(0,4,5,1),(2,3,7,6),(0,1,3,2),(4,6,7,5)]
    o=mesh(name,verts,faces,m,group); o.location=p; o.rotation_euler.z=angle
    if bevel:
        mod=o.modifiers.new('Worn edges','BEVEL'); mod.width=bevel; mod.segments=1
    return o

def rod(name,a,b,r=.015,m=steel,group='Steel'):
    a,b=Vector(a),Vector(b); v=b-a; sides=6
    verts=[(r*math.cos(k*2*math.pi/sides),r*math.sin(k*2*math.pi/sides),z) for z in (0,v.length) for k in range(sides)]
    faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]+[(k,(k+1)%sides,(k+1)%sides+sides,k+sides) for k in range(sides)]
    o=mesh(name,verts,faces,m,group); o.location=a; o.rotation_euler=v.to_track_quat('Z','Y').to_euler(); return o

def shard(name,p,s,m=aggregate,group='Rubble'):
    # Convex chipped triangular chunk, no modifiers or hidden expensive geometry.
    a,b,c=s
    verts=[(-a/2,-b/2,0),(a/2,-b*.32,0),(a*.36,b/2,0),(-a*.4,b*.3,0),(-a*.25,-b*.18,c),(a*.22,b*.1,c*.68)]
    faces=[(3,2,1,0),(0,1,4),(1,5,4),(1,2,5),(2,3,5),(3,4,5),(3,0,4)]
    o=mesh(name,verts,faces,m,group); o.location=p; o.rotation_euler.z=random.uniform(-math.pi,math.pi); return o

walls=[]
def ruined_wall(name,x,y,length,thickness,height,angle=0,group='Walls',damage=.22):
    samples=max(7,round(length/.4)); xs=[-length/2+length*k/samples for k in range(samples+1)]
    hs=[height-random.uniform(0,damage) for _ in xs]
    # Large chips are concentrated at the top, retaining continuous wall silhouettes.
    hs[random.randrange(1,samples)]=height-damage*1.55
    profile=[(-length/2,0),(length/2,0)]+list(reversed(list(zip(xs,hs))))
    n=len(profile)
    verts=[(px,py,pz) for py in (-thickness/2,thickness/2) for px,pz in profile]
    faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    o=mesh(name,verts,faces,concrete,group); o.location=(x,y,0); o.rotation_euler.z=angle
    ca,sa=math.cos(angle),math.sin(angle)
    def P(a,b,c): return (x+a*ca-b*sa,y+a*sa+b*ca,c)
    # Fragmented old paint along the lower part of both faces.
    for side in (-1,1):
        for k in range(4):
            px=-length/2+(k+.5)*length/4; w=length/4*random.uniform(.65,.93)
            z=.47+random.uniform(-.08,.06)
            patch=[P(px-w/2,side*(thickness/2+.006),.18),P(px+w/2,side*(thickness/2+.006),.18),P(px+w*.42,side*(thickness/2+.006),z),P(px-w*.43,side*(thickness/2+.006),z+.10)]
            mesh('Peeling paint',patch,[(0,1,2,3)],plaster,'Patina')
        # Bullet scars and chipped rims; dispersed rather than a repeated stripe.
        for k in range(13):
            px=random.uniform(-length*.46,length*.46); z=random.uniform(.5,height*.82); r=random.uniform(.023,.063)
            vs=[P(px+math.cos(i*math.tau/7)*r,side*(thickness/2+.013),z+math.sin(i*math.tau/7)*r) for i in range(7)]
            mesh('Impact scar',vs,[tuple(range(7))],charcoal,'Patina')
        for k in range(3):
            px=random.uniform(-length*.4,length*.4); z=random.uniform(.7,height*.88)
            for j in range(3):
                nx=px+random.uniform(-.15,.15); nz=z-random.uniform(.10,.25)
                mesh('Fracture', [P(px-.008,side*(thickness/2+.017),z),P(px+.008,side*(thickness/2+.017),z),P(nx+.006,side*(thickness/2+.017),nz),P(nx-.006,side*(thickness/2+.017),nz)],[(0,1,2,3)],charcoal,'Patina')
                px,z=nx,nz
    for k in range(5):
        px=random.uniform(-length*.44,length*.44)
        start=P(px,0,height-damage)
        end=P(px+random.uniform(-.18,.18),random.uniform(-.13,.13),height+random.uniform(.03,.28))
        rod('Exposed bent rebar',start,end)
    for k in range(int(length*5)):
        px=random.uniform(-length*.48,length*.48); side=random.choice((-1,1)); d=side*(thickness/2+random.uniform(.08,.35))
        shard('Wall foot fragments',P(px,d,.01),(random.uniform(.08,.30),random.uniform(.07,.22),random.uniform(.05,.18)),random.choice((aggregate,brick,concrete)))
    return o

for item in layout['objects']:
    n=item['name']; x,z,y=item['position']; sx,sz,sy=item['size']
    if n=='PlatformBase':
        box('Foundation',(x,y,-.16),(sx,sy,.30),concrete,'Foundation')
        continue
    if n=='EntranceHighWall':
        box('Entry high platform',(x,y,z),(sx,sy,sz),concrete,'Entrance')
        for sign in (-1,1):
            box('Entry caution edge',(x+sign*(sx/2-.07),y,z+sz/2+.012),(.11,sy,.025),yellow,'Entrance')
        continue
    if n.startswith('EntranceRail'):
        box(n,(x,y,z),(sx,sy,sz),concrete,'Entrance',bevel=.035)
        continue
    length,thick=max(sx,sy),min(sx,sy); angle=0 if sx>sy else math.pi/2
    ruined_wall(n,x,y,length,thick,sz,angle)
    walls.append(dict(name=n,x=x,y=y,length=length,thickness=thick,angle=angle))

# Scuffed concrete paving with grout and fractured corners, all flush with gameplay ground.
for ix in range(16):
    for iy in range(15):
        x=-9.47+(ix+.5)*18.94/16; y=-8.78+(iy+.5)*17.56/15
        box('Concrete paving',(x,y,.002),(18.94/16-.022,17.56/15-.022,.016),random.choice(tiles),'Paving')
        if random.random()<.09:
            mesh('Floor crack',[(x-.42,y-.15,.012),(x-.41,y-.17,.012),(x+.1,y+.04,.012),(x+.46,y+.42,.012),(x+.44,y+.43,.012),(x+.07,y+.06,.012)],[(0,1,2,3,4,5)],charcoal,'GroundScars')

# Exterior apron and evocative destroyed buildings form a framing silhouette.
box('Wartorn street',(0,0,-.40),(48,43,.42),asphalt,'Surround')
for x in (-10.7,10.7):
    box('Broken sidewalk',(x,1,-.10),(1.0,24,.22),concrete,'Surround')
for x in (-17,-12,12,17):
    for y in (-15,-11,-7,-3,1,5,9,13):
        box('Faded road paint',(x,y,-.182),(.10,1.8,.012),yellow,'Surround')
for bx,by,w,d,h in [(-15,5,7,10,5.2),(14,9,6,8,4.3),(-5,14,9,6,6.2),(6,15,7,6,4.8)]:
    box('Ruined building foundation',(bx,by,-.05),(w,d,.34),concrete,'Backdrop')
    for xx in (-w/2,w/2):
        for yy in (-d/2,0,d/2):
            height=h*random.uniform(.53,1)
            box('Shattered structural pier',(bx+xx,by+yy,height/2),(.47,.52,height),concrete,'Backdrop',bevel=.055)
            for k in range(3): rod('Bent column reinforcement',(bx+xx+random.uniform(-.1,.1),by+yy,height-.1),(bx+xx+.14,by+yy+.12,height+.45))
    # Broken back wall panels between surviving window bays.
    for k in range(4):
        xx=bx-w/2+(k+.5)*w/4
        ruined_wall('Destroyed facade',xx,by+d/2,w/4+.05,.30,random.uniform(2.5,h),0,'Backdrop',.75)
    for level in (2.5,):
        box('Half collapsed floor',(bx-w*.26,by+d*.20,level),(w*.46,d*.50,.23),concrete,'Backdrop',angle=random.uniform(-.04,.04))
        rod('Sagging girder',(bx-w/2,by,level),(bx+w*.3,by+d*.35,level-.8),.13,steel)
    for k in range(45):
        shard('Collapse apron',(bx+random.uniform(-w*.6,w*.6),by+random.uniform(-d*.6,d*.6),.08),(random.uniform(.2,.9),random.uniform(.2,.6),random.uniform(.10,.48)),random.choice((concrete,aggregate,brick)))

# Buckled pipes, rusted girders, sand-colored emergency barricades on the periphery.
for x,y in [(-11.8,-4),(11.8,1),(-8.5,10.5)]:
    rod('Broken service pipe',(x,y,.30),(x+.2,y+2,.40),.17,steel)
    rod('Broken service pipe bend',(x+.2,y+2,.40),(x+.55,y+2.4,.85),.17,steel)
for x,y in [(-5,-11.4),(6,-11.7),(11.6,-8)]:
    box('Road barrier foot',(x,y,.10),(2.0,.64,.22),concrete,'StreetDebris',angle=.1,bevel=.05)
    box('Road barrier',(x,y,.48),(1.85,.27,.72),concrete,'StreetDebris',angle=.1,bevel=.06)
    for k in range(6):
        box('Barrier yellow stripe',(x-.78+k*.3,y-.149,.5),(.14,.017,.5),yellow,'StreetDebris',angle=.1)

props=[dict(asset='BurnedJeep_T2',x=12.8,y=-5.3,size=3.8,angle=-24),dict(asset='BurnedJeep_T2',x=-13.0,y=-9.5,size=3.6,angle=143)]
# Meshy debris sits at wall feet. Footprints stay out of the central walking ribbon.
for i,w in enumerate(walls):
    if i%3: continue
    angle=w['angle']; nx=-math.sin(angle); ny=math.cos(angle)
    props.append(dict(asset='RebarRubble_T2',x=w['x']+nx*.48,y=w['y']+ny*.48,size=.83,angle=math.degrees(angle)+random.uniform(-24,24)))
for x,y,s in [(-11.7,7,2.2),(11.8,6,2.0),(-5,10.1,2.3),(5,10.3,1.8),(-6,-11.4,1.3),(5.6,-10.4,1.4)]:
    props.append(dict(asset='RebarRubble_T2',x=x,y=y,size=s,angle=random.uniform(0,360)))

fire_sites=[(-12.5,-8.9),(12.8,-4.7),(-12,7),(7.7,11)]
for x,y in fire_sites:
    for k in range(22):
        shard('Charred embers',(x+random.uniform(-.7,.7),y+random.uniform(-.5,.5),.04),(random.uniform(.08,.22),.10,random.uniform(.035,.09)),ember if k%3==0 else charcoal,'Embers')

# Join batches by function; no procedural shaders, cameras or lights in the game GLB.
for name,items in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in items: o.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    for o in items:
        if o.modifiers:
            bpy.context.view_layer.objects.active=o
            for mod in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active=items[0]
    if len(items)>1: bpy.ops.object.join()
    o=items[0]; o.name='Aftermath_'+name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)

for o in scene.objects: o.select_set(o.type=='MESH')
bpy.ops.export_scene.gltf(filepath=str(OUT/'Models/MazeAftermath_Architecture.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_yup=True,export_cameras=False,export_lights=False)

def camera(name,pos,target,lens=38):
    d=bpy.data.cameras.new(name); d.lens=lens; d.clip_end=200
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o); o.location=pos
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler(); return o
scene.camera=camera('Aftermath_Overview',(26,-33,32),(0,0,0),44)
camera('Aftermath_Courtyard',(3,-6,2.6),(-4,5,1.0),23)
camera('Aftermath_Entry',(-4,-16,6),(1,1,1),27)
world=bpy.data.worlds.new('Aftermath_DustSky'); world.use_nodes=True; scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs['Color'].default_value=(.43,.49,.55,1); bg.inputs['Strength'].default_value=.5
for name,kind,pos,energy,color,size,target in [('Late dusty sun','SUN',(-12,-8,20),2.2,(1,.79,.57),0,(0,0,0)),('Cold sky','AREA',(0,0,18),4200,(.62,.73,1),24,(0,0,0))]:
    d=bpy.data.lights.new(name,kind); d.energy=energy; d.color=color
    if kind=='AREA': d.shape='DISK'; d.size=size
    else: d.angle=.20
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o); o.location=pos; o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for x,y in fire_sites:
    d=bpy.data.lights.new('Low smoulder','POINT'); d.energy=70; d.color=(1,.16,.025); d.shadow_soft_size=.5
    o=bpy.data.objects.new('Low smoulder',d); scene.collection.objects.link(o); o.location=(x,y,.35)
formats=[i.identifier for i in scene.render.image_settings.bl_rna.properties['file_format'].enum_items]
if 'PNG' in formats: scene.render.image_settings.file_format='PNG'
scene.render.resolution_x=1600; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'; area.spaces.active.overlay.show_overlays=False
manifest=dict(sourceMaze=layout['source'],sourceHash=layout['sha256'],localOrigin=layout['localOrigin'],rootTransform=layout['root'],
    glbUnityCorrectionEuler=[0,180,0],mazeCells=[5,5],cellSize=layout['cellSize'],wallCount=len(walls),
    props=props,fireSites=fire_sites,materials='concrete_wall_008 CC0, existing project texture; other materials authored in Blender',
    meshes=[dict(name=o.name,vertices=len(o.data.vertices),triangles=sum(len(p.vertices)-2 for p in o.data.polygons)) for o in scene.objects if o.type=='MESH'])
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MazeAftermath.blend'))
print(json.dumps(dict(scene=scene.name,wallCount=len(walls),batches=len(manifest['meshes']),triangles=sum(m['triangles'] for m in manifest['meshes']),props=len(props))))
