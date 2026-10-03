"""Blender-driven original 3D sprite production for MET 0.4.

Run with:
  Blender --background --python tools/make_art040.py -- --preview
  Blender --background --python tools/make_art040.py -- --all

The PNGs are runtime assets. No .blend files are written into assets/world040.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "assets" / "world040"
SOURCE = ROOT / "art_source" / "visual040"
TIER_PREVIEW_DIR = SOURCE / "laisheng_tier_preview"
CATALOG_PATH = ROOT / "content" / "presentation040" / "art_catalog.json"

# All game-facing buildings share this orthographic world scale. Geometry is
# authored in world units; large late-game facilities therefore remain larger.
ORTHO_WORLD_UNITS = 64.0
PROP_VIEW_UNITS = 20.0
MODULE_VIEW_UNITS = 26.0
TERRAIN_VIEW_UNITS = 42.0
COURTYARD_VIEW_UNITS = 26.0
CAMERA_VECTOR = (9.0, -12.0, 13.2)
ANCHOR_NDC_Y = 0.36  # normalized bottom-origin coordinate => 64% from top
BUILDING_SIZE = 768
PROP_SIZE = 256

PALETTE = {
    # Laisheng uses mineral light walls, blue-gray equipment and small clay
    # accents. The colors stay distinct at map scale without reading as toys.
    "ivory": "E4E0D6", "cream": "EEEADF", "sand": "BBB19F",
    "shadow_wall": "CAC4B7", "terracotta": "A8664D", "tile": "B87A5E",
    "red_brick": "79544E", "dark_brick": "514749", "wood": "66564E",
    "wood_light": "92796A", "metal": "526A79", "metal_dark": "344653",
    "metal_light": "8297A0", "glass": "547980", "glass_light": "89A7A5",
    "trim": "DCD7CB", "stone": "929894", "stone_dark": "596367",
    "green": "687E6A", "green_light": "9BAE8A", "green_dark": "465B4C",
    "leaf": "657E69", "leaf_light": "9CAF8A", "leaf_dark": "405648",
    "soil": "6E675A", "soil_light": "96876C", "crop": "B1A47B",
    "water": "6F8E94", "gold": "B69765", "ochre": "B79D6F", "cream_sign": "E2DED2",
}

_mats = {}


def _linear_channel(v):
    c = int(v, 16) / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mat(key, roughness=0.78, metallic=0.0):
    token = (key, roughness, metallic)
    if token in _mats:
        return _mats[token]
    hex_color = PALETTE.get(key, key).lstrip("#")
    rgb = tuple(_linear_channel(hex_color[i:i + 2]) for i in (0, 2, 4))
    if key in ("metal", "metal_dark", "metal_light") and metallic == 0.0:
        metallic = 0.28 if key != "metal_dark" else 0.20
        roughness = 0.62
    m = bpy.data.materials.new("mat_" + str(key))
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*rgb, 1.0)
    bs.inputs["Roughness"].default_value = roughness
    bs.inputs["Metallic"].default_value = metallic
    _mats[token] = m
    return m


def clear_scene():
    global _mats
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    _mats = {}


def bevel_obj(obj, amount=0.06, segments=2):
    if amount > 0:
        mod = obj.modifiers.new("soft hand-finished edges", "BEVEL")
        mod.width = amount
        mod.segments = segments
        mod.limit_method = "ANGLE"
        norm = obj.modifiers.new("weighted corner normals", "WEIGHTED_NORMAL")
        norm.keep_sharp = True
    return obj


def box(name, loc, dims, material, bevel=0.045, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.rotation_euler = rot
    o.data.materials.append(mat(material))
    return bevel_obj(o, min(bevel, min(dims) * 0.09))


def mesh(name, verts, faces, material, bevel=0.0):
    data = bpy.data.meshes.new(name + "Mesh")
    data.from_pydata(verts, [], faces)
    data.materials.append(mat(material))
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return bevel_obj(obj, bevel, 2)


def cylinder(name, loc, radius, depth, material, vertices=12, bevel=0.035, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        end_fill_type="NGON", location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat(material, metallic=0.06 if material in ("metal", "metal_dark", "metal_light") else 0))
    return bevel_obj(o, min(bevel, radius * .25, depth * .1))


def ico(name, loc, scale, material, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.data.materials.append(mat(material))
    return o


def gable_roof(name, x, y, z, w, d, rise, material="terracotta", ridge_axis="x", tiles=True):
    # A hand-built roof shell, ridge along x. Slightly proud eaves give each
    # building a readable silhouette at the map's normal zoom.
    e = 0.28
    verts = [(-w/2-e,-d/2-e,z), (w/2+e,-d/2-e,z),
             (w/2+e,d/2+e,z), (-w/2-e,d/2+e,z),
             (-w/2-e,0,z+rise), (w/2+e,0,z+rise),
             (-w/2-e,-d/2-e,z-.22), (w/2+e,-d/2-e,z-.22),
             (w/2+e,d/2+e,z-.22), (-w/2-e,d/2+e,z-.22)]
    verts = [(vx+x, vy+y, vz) for vx,vy,vz in verts]
    mesh(name+" roof shell", verts,
         [(0,4,5,1), (3,2,5,4), (0,1,7,6), (1,2,8,7), (2,3,9,8), (3,0,6,9)], material)
    box(name+" front fascia", (x, y-d/2-.29, z-.02), (w+.56,.20,.42), "wood_light", .035)
    box(name+" rear fascia", (x, y+d/2+.29, z-.02), (w+.56,.20,.42), "wood", .035)
    box(name+" ridge cap", (x,y,z+rise+.04),(w+.22,.34,.22),"tile",.06)
    if tiles:
        for side in (-1, 1):
            for i in range(1, 5):
                yy = side * d * i / 10
                zz = z + rise * (1 - i/5) + .12
                slope = -math.atan2(rise, d/2)
                box(name+" tile course", (x,y+yy,zz), (w+.42,.08,.10), "tile" if material == "terracotta" else material,
                    .015, rot=(slope if side < 0 else -slope,0,0))


def flat_roof(name, x,y,z,w,d,material="metal", parapet=0.7):
    box(name+" roof slab",(x,y,z),(w+.45,d+.45,.5),material,.07)
    for xx in (-1,1):
        box(name+" parapet",(x+xx*(w/2),y,z+parapet/2),(0.45,d+.45,parapet),"ivory",.05)
    for yy in (-1,1):
        box(name+" parapet",(x,y+yy*(d/2),z+parapet/2),(w+.45,.45,parapet),"ivory",.05)


def wall(name, x, y, w, d, h, material="ivory", roof="terracotta", style="gable"):
    box(name+" warm plaster walls",(x,y,h/2),(w,d,h),material,.09)
    # String course and corner pilasters make the wall read as crafted masonry.
    box(name+" lower stone course",(x,y-d/2-.03,.65),(w,.16,.48),"sand",.035)
    box(name+" front eave trim",(x,y-d/2-.05,h-.32),(w+.24,.14,.20),"trim",.025)
    if style == "gable":
        rise=max(2.8,h*.34)
        gable_roof(name,x,y,h,w,d,rise,roof)
        # Close the two short gable ends with matching plaster so the roof
        # silhouette reads as a finished building from the oblique camera.
        for side in (-1,1):
            end_x=x+side*(w/2+.03)
            mesh(name+(" west gable infill" if side<0 else " east gable infill"),
                 [(end_x,y-d/2-.03,h-.05),(end_x,y+d/2+.03,h-.05),
                  (end_x,y,h+rise-.12)],[(0,1,2)],material)
            box(name+" attic vent",(end_x+side*.04,y,h+rise*.47),(.12,1.25,.62),"wood",.08)
    else:
        flat_roof(name,x,y,h,w,d,roof,0.75)


def front_windows(name, x, y, z, w, h, count, levels=1, style="plain"):
    face_y = y - .5
    for row in range(levels):
        rz = z + (row + .55) * h / levels
        for i in range(count):
            wx = x - w/2 + (i+.5) * w/count
            ww = min(1.35, w/count*.48)
            wh = min(1.70, h/levels*.38)
            if style == "arch":
                box(name+" recessed shop window",(wx,face_y-.03,rz),(ww+.20,.12,wh+.25),"sand",.07)
                box(name+" glazed opening",(wx,face_y-.105,rz+.04),(ww,.10,wh),"glass",.04)
                box(name+" transom",(wx,face_y-.18,rz),(ww,.10,.10),"trim",.015)
            else:
                box(name+" window reveal",(wx,face_y-.045,rz),(ww+.20,.14,wh+.22),"sand",.035)
                box(name+" blue-green glass",(wx,face_y-.125,rz),(ww,.11,wh),"glass_light" if i%3==0 else "glass",.025)
                box(name+" stone sill",(wx,face_y-.22,rz-wh/2-.10),(ww+.28,.24,.12),"trim",.025)
                box(name+" lintel",(wx,face_y-.17,rz+wh/2+.12),(ww+.23,.16,.13),"trim",.02)
                box(name+" central muntin",(wx,face_y-.20,rz),(.055,.08,wh),"trim",.012)


def side_windows(name, x, y, z, d, h, count, levels=1):
    face_x = x + .5
    for row in range(levels):
        rz = z + (row + .55) * h / levels
        for i in range(count):
            wy = y - d/2 + (i+.5)*d/count
            ww = min(1.45,d/count*.45)
            wh = min(1.65,h/levels*.38)
            box(name+" side reveal",(face_x+.04,wy,rz),(.13,ww+.18,wh+.20),"sand",.03)
            box(name+" side glass",(face_x+.12,wy,rz),(.10,ww,wh),"glass",.025)
            box(name+" side sill",(face_x+.20,wy,rz-wh/2-.10),(.22,ww+.23,.12),"trim",.02)


def door(name, x, y, z, height=2.3, width=1.25, color="wood"):
    fy = y - .72
    box(name+" entry surround",(x,fy-.03,z+height/2),(width+.50,.18,height+.40),"trim",.055)
    box(name+" recessed door",(x,fy-.15,z+height/2),(width,.16,height),color,.045)
    box(name+" door panel",(x,fy-.25,z+height/2),(width*.60,.08,height*.72),"wood_light",.025)
    cylinder(name+" brass handle",(x+width*.34,fy-.34,z+height*.52),.07,.08,"gold",10,.01,(math.pi/2,0,0))
    box(name+" entry step",(x,fy-.42,z+.12),(width+1.0,.85,.24),"stone",.035)


def porch(name,x,y,z,w=4.5,d=1.7):
    box(name+" porch deck",(x,y-d/2,z+.08),(w,d,.18),"stone",.035)
    for xx in (-w*.42,w*.42):
        cylinder(name+" porch column",(x+xx,y-d+.15,z+1.30),.13,2.6,"ivory",10,.02)
    box(name+" porch lintel",(x,y-d+.15,z+2.55),(w+.22,.20,.20),"wood_light",.035)


def chimney(name,x,y,z,h=3.4):
    box(name+" brick chimney",(x,y,z+h/2),(.9,.9,h),"red_brick",.06)
    box(name+" chimney cap",(x,y,z+h+.08),(1.18,1.18,.25),"stone_dark",.06)


def leaf_mass(name,loc,scale,material,rotation=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=1,location=loc)
    obj=bpy.context.object
    obj.name=name
    obj.scale=scale
    obj.rotation_euler[2]=rotation
    obj.data.materials.append(mat(material))
    for face in obj.data.polygons:
        face.use_smooth=False
    return obj


def tree(name,x,y,z=0,height=8,crown=3.2,conifer=False):
    bpy.ops.mesh.primitive_cone_add(vertices=9, radius1=.48, radius2=.24,
                                    depth=height*.64, location=(x,y,z+height*.32))
    trunk=bpy.context.object
    trunk.name=name+" tapered warm bark"
    trunk.data.materials.append(mat("wood"))
    if conifer:
        for j,(r,h) in enumerate(((crown,height*.44),(crown*.80,height*.40),
                                  (crown*.59,height*.36),(crown*.36,height*.30))):
            base=height*(.22+j*.145)
            bpy.ops.mesh.primitive_cone_add(vertices=9, radius1=r, radius2=.08, depth=h,
                                            location=(x,y,z+base+h*.48))
            o=bpy.context.object
            o.name=name+" layered evergreen bough"
            o.data.materials.append(mat(("leaf_dark","leaf","green","leaf_light")[j]))
    else:
        clusters=(
            (0.00, 0.00, .77, .72, .58, .27, "leaf"),
            (-.46,-.02, .70, .53, .48, .22, "green"),
            (.45, .08, .73, .52, .48, .23, "leaf_light"),
            (-.12,-.40, .68, .48, .46, .20, "leaf_light"),
            (.14, .39, .71, .50, .47, .22, "leaf_dark"),
            (-.27, .06, .94, .43, .40, .19, "leaf_light"),
            (.22,-.03, .96, .46, .40, .20, "green_light"),
        )
        for i,(dx,dy,dz,sx,sy,sz,color) in enumerate(clusters):
            leaf_mass(name+" connected crown lobe",
                      (x+dx*crown,y+dy*crown,z+height*dz),
                      (crown*sx,crown*sy,height*sz),color,(i%3-1)*.18)


def shrub(name,x,y,z=0,r=1.3):
    for dx,dy,s in ((-.55,0,.78),(0,.18,1),( .52,-.08,.72)):
        ico(name+" leaf cluster",(x+dx*r,y+dy*r,z+r*.50),(r*.63*s,r*.55*s,r*.60*s),"green_light" if dx==0 else "leaf",1)


def rock(name,x,y,z=0,s=1.0,material="stone"):
    ico(name+" chipped stone",(x,y,z+s*.68),(s*1.15,s*.92,s*.78),material,1)
    ico(name+" stone facet",(x-s*.38,y+s*.12,z+s*.59),(s*.62,s*.65,s*.67),"stone_dark",1)


def path_segment(name,x,y,z,length,width,material="sand",rot=0):
    box(name,(x,y,z),(length,width,.10),material,.03,(0,0,rot))


def fence_run(name,x,y,z,length,material="wood_light",axis="x",posts=4):
    if axis=="x":
        for i in range(posts):
            xx=x-length/2+i*length/(posts-1)
            box(name+" picket",(xx,y,z+1.0),(.20,.22,2),"wood",.025)
        for zz in (.60,1.48): box(name+" rail",(x,y,z+zz),(length,.18,.16),material,.025)
    else:
        for i in range(posts):
            yy=y-length/2+i*length/(posts-1)
            box(name+" picket",(x,yy,z+1.0),(.22,.20,2),"wood",.025)
        for zz in (.60,1.48): box(name+" rail",(x,y,z+zz),(.18,length,.16),material,.025)


def greenhouse(name,x,y,w,d,h=6):
    box(name+" stone sill",(x,y,.35),(w,d,.55),"sand",.07)
    wallz=.65
    # Repeated glass bays with a pitched glazing roof and thin green-gray frames.
    for side in (-1,1):
        yy=y+side*d*.25
        box(name+" glazed long side",(x,yy,wallz+h*.40),(w-.35,.12,h*.72),"glass_light",.025)
        for i in range(7):
            xx=x-w*.43+i*w*.143
            box(name+" mullion",(xx,yy-.10*side,wallz+h*.40),(.11,.12,h*.72),"metal",.02)
    for j in range(7):
        xx=x-w*.43+j*w*.143
        box(name+" roof arch rib",(xx,y,wallz+h*.72),(.13,d*1.08,.16),"metal",.025)
    mesh(name+" glazed roof planes",[(x-w*.5,y-d*.51,wallz+h*.64),(x-w*.5,y,wallz+h*1.06),
                                      (x+w*.5,y,wallz+h*1.06),(x+w*.5,y-d*.51,wallz+h*.64),
                                      (x-w*.5,y,wallz+h*1.06),(x-w*.5,y+d*.51,wallz+h*.64),
                                      (x+w*.5,y+d*.51,wallz+h*.64),(x+w*.5,y,wallz+h*1.06)],
         [(0,1,2,3),(4,5,6,7)],"glass")


def sawtooth_roof(name,x,y,z,w,d,bays=5):
    bay=w/bays
    for i in range(bays):
        bx=x-w/2+bay*(i+.5)
        box(name+" raised monitor",(bx,y,z+2.0),(bay*.48,d*.90,3.6),"metal_dark",.035)
        box(name+" clerestory glass",(bx,y-d*.47,z+2.05),(bay*.38,.12,2.4),"glass_light",.02)
        mesh(name+" sawtooth roof facet",[(bx-bay*.50,y-d*.50,z),(bx-bay*.18,y-d*.50,z+3.7),
                                          (bx-bay*.18,y+d*.50,z+3.7),(bx-bay*.50,y+d*.50,z)],[(0,1,2,3)],"metal")
        mesh(name+" roof slope",[(bx-bay*.18,y-d*.50,z+3.7),(bx+bay*.50,y-d*.50,z),
                                  (bx+bay*.50,y+d*.50,z),(bx-bay*.18,y+d*.50,z+3.7)],[(0,1,2,3)],"metal_light")


def silo(name,x,y,z=0,r=2.1,h=7,material="metal_light"):
    cylinder(name+" silo shell",(x,y,z+h/2),r,h,material,16,.06)
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=r*1.02, radius2=.15, depth=1.6,
                                    location=(x,y,z+h+.8))
    o=bpy.context.object; o.name=name+" conical cap";o.data.materials.append(mat("metal"))
    for zz in (z+1,z+h-1):
        cylinder(name+" band",(x,y,zz),r+.05,.12,"metal_dark",16,.015)


def thermal_tower(name,x,y,z=0,bottom_radius=3.0,top_radius=1.8,height=12.0):
    """Closed-loop heat rejection tower with a clear flared service silhouette."""
    bpy.ops.mesh.primitive_cone_add(vertices=20,radius1=bottom_radius,radius2=top_radius,
                                    depth=height,location=(x,y,z+height*.5))
    shell=bpy.context.object
    shell.name=name+" flared exchanger shell"
    shell.data.materials.append(mat("metal_light"))
    cylinder(name+" lower service band",(x,y,z+1.1),bottom_radius+.06,.18,"metal_dark",20,.015)
    cylinder(name+" upper service rim",(x,y,z+height-.45),top_radius+.12,.24,"metal",20,.02)
    cylinder(name+" outlet grille",(x,y,z+height+.04),top_radius*.72,.12,"metal_dark",20,.01)


def rooftop_turbine(name,x,y,roof_z,radius=2.2):
    """Compact roof-mounted three-blade ventilation turbine."""
    cylinder(name+" raised bearing mast",(x,y,roof_z+.9),.16,1.8,"metal_dark",10,.018)
    center_z=roof_z+1.95
    cylinder(name+" rotor hub",(x,y-.10,center_z),.42,.68,"metal_light",12,.035,(math.pi/2,0,0))
    for i in range(3):
        angle=math.tau*i/3
        offset=radius*.52
        box(name+" rotor blade",(x+math.sin(angle)*offset,y-.12,center_z+math.cos(angle)*offset),
            (.30,.12,radius*1.08),"metal",.045,(0,angle,0))


def sign_mark(name,x,y,z,material="water",kind="circle"):
    # Abstract embossed seal; avoids built-in text and real-world organization marks.
    if kind=="cross":
        box(name+" sign field",(x,y,z),(.90,.16,.90),"cream_sign",.12)
        box(name+" vertical glyph",(x,y-.11,z),(.18,.10,.57),material,.06)
        box(name+" horizontal glyph",(x,y-.12,z),(.57,.10,.18),material,.06)
    else:
        cylinder(name+" sign medallion",(x,y,z),.55,.18,material,16,.025,(math.pi/2,0,0))
        cylinder(name+" sign core",(x,y-.12,z),.22,.10,"cream_sign",16,.02,(math.pi/2,0,0))


def place_house(name,x,y,w=10,d=8,h=6.5,wallmat="ivory",roofmat="terracotta",style="gable",front=True):
    wall(name,x,y,w,d,h,wallmat,roofmat,style)
    # front_windows' y argument is the point just behind its frame offset.
    # The old centre-based call embedded the glazing inside the plaster box.
    front_windows(name,x,y-d/2+.42,0,w,h,max(2,int(w/4.3)),1)
    side_windows(name,x+w/2,y,0,d,h,2,1)
    door(name,x,y-d*.53,0,2.1,1.0)


def fern_frond(name, start, direction, length, width, material):
    """A restrained, pinnate frond built as a small shaded mesh ribbon."""
    from mathutils import Vector
    origin = Vector(start)
    axis = Vector(direction).normalized()
    side = Vector((-axis.y, axis.x, 0.0))
    if side.length < 0.01:
        side = Vector((1.0, 0.0, 0.0))
    side.normalize()
    verts = []
    faces = []
    segments = 7
    stem = [origin + axis * (length * i / segments) for i in range(segments + 1)]
    for i in range(segments):
        t0, t1 = i / segments, (i + 1) / segments
        taper0 = max(.01, math.sin(math.pi * min(.98, t0 + 0.07))) ** 0.72
        taper1 = max(.01, math.sin(math.pi * min(.98, t1 + 0.03))) ** 0.72
        for sign in (-1.0, 1.0):
            tip0 = stem[i] + side * (sign * width * taper0) + Vector((0, 0, width * taper0 * .22))
            tip1 = stem[i + 1] + side * (sign * width * taper1) + Vector((0, 0, width * taper1 * .22))
            base = len(verts)
            verts.extend([stem[i], stem[i + 1], tip1, tip0])
            faces.append((base, base + 1, base + 2, base + 3))
    mesh(name + " pinnate leaflets", verts, faces, material)
    axis_end = origin + axis * length
    cylinder(name + " flexible rachis", (origin + axis_end) * .5, .025, length,
             "green_dark", 7, .003, axis.to_track_quat("Z", "Y").to_euler())


def alien_tree(name, x, y, z=0, height=8, crown=3.2, upright=False):
    """Fern-like colony vegetation with muted fronds and a tapered trunk."""
    bpy.ops.mesh.primitive_cone_add(vertices=9, radius1=.32, radius2=.11,
                                    depth=height * .74, location=(x, y, z + height * .37))
    trunk = bpy.context.object
    trunk.name = name + " narrow mineral-bark trunk"
    trunk.data.materials.append(mat("wood"))
    if upright:
        # Upright conifers still need a real crown at map scale: five whorls
        # overlap into a tapered canopy rather than a pole with tiny tufts.
        layers = ((.34, .48), (.47, .70), (.60, .88), (.73, .76), (.85, .52))
    else:
        layers = ((.36, .52), (.49, .76), (.62, .96), (.75, .84), (.87, .58))
    for layer, (level, spread) in enumerate(layers):
        count = 7 if upright else 8
        for i in range(count):
            angle = math.tau * i / count + layer * .24
            reach = crown * spread * (0.92 + .08 * ((i + layer) % 2))
            if upright:
                reach *= .96
                direction = (math.cos(angle) * .82, math.sin(angle) * .82, .58)
            else:
                direction = (math.cos(angle), math.sin(angle), .24)
            base = (x, y, z + height * level)
            fern_frond(name + " radial fern", base, direction, reach, crown * (.22 if upright else .20),
                       ("leaf_dark", "leaf", "leaf_light")[(i + layer) % 3])
    # A small crown bud breaks the silhouette without bright ornament.
    leaf_mass(name + " central crown", (x, y, z + height * .96),
              (crown * .19, crown * .19, height * .10), "green")


def alien_shrub(name, x, y, z=0, r=1.3):
    for i in range(5):
        angle = math.tau * i / 5
        start = (x, y, z + r * .34)
        reach = r * (.72 + .08 * (i % 2))
        direction = (math.cos(angle), math.sin(angle), .30)
        fern_frond(name + " ground fern", start, direction, reach, r * .28,
                   "leaf_light" if i % 3 == 0 else "leaf")
    ico(name + " lichen cushion", (x, y, z + r * .42), (r * .34, r * .34, r * .20), "green_dark", 1)


def service_cassette(name, x, y, z=0, scale=1.0):
    """External power, heat and water equipment for a self-servicing colony."""
    w, d, h = 2.8 * scale, 1.8 * scale, 2.4 * scale
    cy = y - d * .5
    box(name + " equipment skid", (x, y, .18 * scale), (w + .45 * scale, d + .25 * scale, .36 * scale), "metal_dark", .035)
    box(name + " insulated cabinet", (x, y, z + h * .5), (w, d, h), "metal_light", .035)
    box(name + " access panel", (x, cy - .06 * scale, z + h * .51), (w * .68, .10 * scale, h * .60), "metal_dark", .018)
    for i in range(4):
        box(name + " heat exchanger fin", (x - w * .29 + i * w * .19, cy - .13 * scale, z + h * .48),
            (.055 * scale, .09 * scale, h * .43), "metal_light", .008)
    cylinder(name + " service riser", (x + w * .62, y, z + h * .66), .24 * scale, h * .78,
             "metal", 12, .02)
    cylinder(name + " pressure vessel", (x + w * .91, y + .22 * scale, z + h * .47),
             .34 * scale, h * .86, "metal_light", 14, .025)
    for zz in (z + h * .28, z + h * .73):
        cylinder(name + " vessel collar", (x + w * .91, y + .22 * scale, zz),
                 .37 * scale, .10 * scale, "metal_dark", 14, .008)


def habitat_module(name, x, y, w=9, d=7, h=4.2, windows=3, accent="terracotta"):
    """Low light-wall prefab with visible joints, sheltered openings and skids."""
    front = y - d * .5
    box(name + " raised footing", (x, y, .24), (w + .50, d + .45, .48), "metal_dark", .035)
    box(name + " insulated light-wall shell", (x, y, h * .5 + .48), (w, d, h), "ivory", .025)
    box(name + " lower impact rail", (x, front - .08, .85), (w - .18, .12, .24), "metal", .018)
    box(name + " narrow roof eave", (x, y, h + .63), (w + .42, d + .40, .22), "metal_dark", .022)
    box(name + " blue-gray roof cap", (x, y, h + .50), (w + .16, d + .16, .16), "metal_light", .018)
    for i in range(1, max(2, windows)):
        xx = x - w * .5 + i * w / windows
        box(name + " panel seam", (xx, front - .055, h * .48 + .48), (.035, .075, h * .90), "shadow_wall", .006)
    for i in range(windows):
        xx = x - w * .5 + (i + .5) * w / windows
        zz = 2.35 if h > 3.8 else h * .53
        box(name + " window gasket", (xx, front - .10, zz), (1.10, .11, 1.05), "metal_dark", .035)
        box(name + " insulated glazing", (xx, front - .17, zz), (.88, .08, .83), "glass", .018)
    # The east-facing return is visible from the shared map camera and gets
    # the same small, deeply set openings as the front wall.
    side_x = x + w * .5 + .07
    for i in range(max(2, int(d / 2.8))):
        yy = y - d * .5 + (i + .5) * d / max(2, int(d / 2.8))
        box(name + " return window gasket", (side_x, yy, h * .55 + .48), (.13, .94, 1.0), "metal_dark", .02)
        box(name + " return glazing", (side_x + .08, yy, h * .55 + .48), (.07, .73, .77), "glass", .014)
    door(name + " pressure vestibule", x + w * .27, front - .15, .50, 2.6, 1.15, "metal_dark")
    box(name + " shallow awning", (x + w * .27, front - .62, 3.05), (2.4, 1.25, .16), accent, .022)
    # Roof cassettes and a short vent make the shell read as occupied equipment.
    for i in range(2):
        box(name + " roof collector", (x - w * .23 + i * w * .26, y, h + .80),
            (w * .20, d * .43, .09), "metal", .012, rot=(.12, 0, 0))
    cylinder(name + " filtered air riser", (x - w * .37, y + d * .20, h + .90), .20, .74, "metal_dark", 10, .018)


def streetfront_block(name, x, y, w=10, d=12, floors=2, retail=True, core=False, accent="terracotta"):
    """Permanent town block with small regular windows and a continuous eave."""
    floor_h = 3.0
    ground_h = 3.25 if retail else 2.85
    h = ground_h + max(0, floors - 1) * floor_h
    front = y - d * .5
    box(name + " footing course", (x, y, .34), (w + .32, d + .28, .68), "stone_dark", .025)
    box(name + " mineral wall volume", (x, y, h * .5 + .68), (w, d, h), "cream", .025)
    box(name + " continuous plinth", (x, front - .07, 1.02), (w, .16, .48), "stone", .014)
    # Ground-floor openings form a real street frontage rather than a blank box.
    bays = max(2, int(w / 4.0))
    for i in range(bays):
        bx = x - w * .5 + w * (i + .5) / bays
        bw = w / bays * (.70 if retail else .43)
        wz = ground_h * .52 + .50
        box(name + " recessed shop opening", (bx, front - .10, wz), (bw + .20, .14, ground_h * .64), "metal_dark", .025)
        box(name + (" clear shop glazing" if retail else " service window"),
            (bx, front - .19, wz), (bw, .08, ground_h * .51), "glass", .016)
        if retail and i % 2 == 1:
            box(name + " narrow entry leaf", (bx + bw * .34, front - .25, wz), (.58, .08, ground_h * .52), "metal_light", .012)
    # Upper floors have small recessed openings, spaced like repeatable housing.
    for level in range(1, floors):
        z = ground_h + (level - .45) * floor_h + .68
        count = max(3, int(w / 2.25))
        for i in range(count):
            wx = x - w * .5 + (i + .5) * w / count
            ww = min(1.12, w / count * .50)
            box(name + " upper window reveal", (wx, front - .085, z), (ww + .12, .14, 1.28), "metal_dark", .018)
            box(name + " upper window", (wx, front - .17, z), (ww, .07, 1.02), "glass_light", .012)
            box(name + " projecting sill", (wx, front - .24, z - .67), (ww + .20, .19, .10), "trim", .012)
        if not retail and (level % 2 == 1 or floors <= 3):
            # Shallow balconies and their shade lips break up long residential elevations.
            for bx in (x - w * .27, x + w * .27):
                bz = ground_h + (level - 1) * floor_h + .72
                box(name + " recessed balcony slab", (bx, front - .42, bz), (w * .29, .78, .15), "stone", .018)
                box(name + " balcony handrail", (bx, front - .78, bz + .53), (w * .29, .07, .09), "metal_dark", .008)
                for post in (-1, 0, 1):
                    box(name + " balcony baluster", (bx + post * w * .105, front - .78, bz + .28),
                        (.045, .06, .48), "metal", .006)
                box(name + " balcony planter", (bx, front - .39, bz + .18), (w * .20, .30, .28), "stone_dark", .018)
                alien_shrub(name + " balcony lichen planter", bx, front - .39, bz + .32, .24)
    for xx in (x - w * .5 + .20, x + w * .5 - .20):
        box(name + " facade pilaster", (xx, front - .11, h * .51 + .68), (.18, .18, h), "metal", .014)
    for z in (ground_h + .58, h + .68):
        box(name + " floor band", (x, front - .14, z), (w + .10, .19, .13), "trim", .012)
    # The visible right return receives a matching vertical window rhythm.
    side_x = x + w * .5 + .075
    side_count = max(2, int(d / 3.1))
    for level in range(floors):
        z = 1.78 if level == 0 else ground_h + (level - .45) * floor_h + .68
        for i in range(side_count):
            wy = y - d * .5 + (i + .5) * d / side_count
            ww = min(1.08, d / side_count * .52)
            wh = 1.12 if level else 1.25
            box(name + " return window reveal", (side_x, wy, z), (.15, ww + .14, wh + .16), "metal_dark", .018)
            box(name + " return window", (side_x + .085, wy, z), (.07, ww, wh), "glass_light", .012)
            box(name + " return sill", (side_x + .15, wy, z - wh * .55), (.18, ww + .20, .09), "trim", .01)
    # A low parapet frames the roof and grouped service gear makes it feel used.
    box(name + " mineral roof membrane", (x, y, h + .72), (w + .12, d + .12, .14), "metal_light", .018)
    for xx in (x - w * .5, x + w * .5):
        box(name + " roof parapet", (xx, y, h + 1.04), (.24, d + .38, .58), "metal_dark", .018)
    for yy in (y - d * .5, y + d * .5):
        box(name + " roof parapet", (x, yy, h + 1.04), (w + .38, .24, .58), "metal_dark", .018)
    box(name + " continuous metal eave", (x, y, h + .73), (w + .30, d + .30, .18), "metal_light", .018)
    box(name + " roof access pod", (x + w * .24, y + d * .18, h + 1.80), (w * .22, d * .20, 1.10), "metal_light", .028)
    # Heat pumps, vents and a short band of solar glass are grouped beside the stair pod.
    for i in range(2):
        box(name + " roof heat pump", (x - w * .23 + i * w * .16, y + d * .16, h + .98),
            (w * .10, d * .16, .44), "metal", .025)
    for i in range(3):
        box(name + " roof solar strip", (x - w * .28 + i * w * .16, y - d * .10, h + .91),
            (w * .12, d * .20, .07), "metal_dark", .008, rot=(.07, -.08, 0))
    if retail:
        box(name + " street canopy", (x, front - .62, ground_h + .18), (w + .05, 1.05, .15), accent, .018)
        for xx in (x - w * .38, x + w * .38):
            cylinder(name + " canopy support", (xx, front - .56, ground_h * .50), .075, ground_h, "metal_dark", 8, .008)
    else:
        box(name + " sheltered resident entry canopy", (x, front - .52, ground_h - .13),
            (w * .34, 1.05, .14), "terracotta", .016)
    if core:
        box(name + " vertical circulation core", (x + w * .36, y + d * .28, h + 2.0),
            (w * .20, d * .22, 2.4), "metal", .035)
        for level in range(floors):
            box(name + " core glazing", (x + w * .36, front - .17, ground_h + level * floor_h + 1.9),
                (1.0, .09, 1.85), "glass", .012)
    return h + (3.2 if core else 2.0)


def equipment_hall(name, x, y, w, d, h, doors=2, roof_style="flat"):
    """Panelized industrial shell with exposed but orderly service equipment."""
    front = y - d * .5
    box(name + " concrete service sill", (x, y, .28), (w + .5, d + .45, .56), "stone_dark", .035)
    box(name + " insulated metal wall", (x, y, h * .5 + .56), (w, d, h), "metal_light", .025)
    box(name + " roof edge cap", (x, y, h + .70), (w + .55, d + .52, .28), "metal_dark", .022)
    # Shallow wall ribs and ventilation slots supply a believable module scale.
    for i in range(1, max(2, int(w / 4))):
        xx = x - w * .5 + i * w / max(2, int(w / 4))
        box(name + " facade joint", (xx, front - .055, h * .50 + .56), (.07, .09, h * .92), "metal", .008)
    for i in range(doors):
        xx = x - w * .5 + (i + .5) * w / doors
        box(name + " loading recess", (xx, front - .11, min(3.4, h * .42) + .4), (w / doors * .55, .16, min(5.1, h * .70)), "metal_dark", .025)
        box(name + " insulated shutter", (xx, front - .20, min(3.4, h * .42) + .4), (w / doors * .48, .08, min(4.8, h * .65)), "metal", .015)
    return h + 1.6


def retail_podium(name,x,y,w=38,d=17,h=4.4):
    """Broad glazed shop base with an inhabited terrace and a shaded walk."""
    front=y-d*.5
    box(name+" stone footing",(x,y,.34),(w+.35,d+.35,.68),"stone_dark",.025)
    box(name+" warm retail podium",(x,y,h*.5+.34),(w,d,h),"cream",.03)
    bays=max(7,int(w/4.5))
    for i in range(bays):
        bx=x-w*.5+w*(i+.5)/bays
        bw=w/bays*.74
        z=h*.48+.42
        box(name+" deep shop reveal",(bx,front-.11,z),(bw+.22,.16,h*.62),"metal_dark",.025)
        box(name+" full-height shop glass",(bx,front-.21,z),(bw,.08,h*.53),"glass_light" if i%3==0 else "glass",.018)
        box(name+" glazed mullion",(bx,front-.27,z),(.09,.07,h*.52),"metal_light",.012)
    # Visible return bays prevent the wide block from becoming a blank side wall.
    side_x=x+w*.5+.08
    for i in range(3):
        yy=y-d*.36+i*d*.36
        box(name+" arcade return reveal",(side_x,yy,h*.5+.42),(.16,3.0,h*.62),"metal_dark",.025)
        box(name+" arcade return glazing",(side_x+.08,yy,h*.5+.42),(.08,2.72,h*.53),"glass",.018)
    box(name+" terracotta shade canopy",(x,front-.72,h*.78+.48),(w+.4,1.30,.20),"terracotta",.025)
    for i in range(9):
        xx=x-w*.45+i*w*.1125
        cylinder(name+" canopy arcade pier",(xx,front-.67,h*.37+.34),.095,h*.72,"metal_dark",10,.01)
    box(name+" roof terrace slab",(x,y,h+.67),(w+.42,d+.42,.34),"metal_light",.025)
    for yy in (y-d*.5,y+d*.5):
        box(name+" terrace parapet",(x,yy,h+1.0),(w+.48,.25,.65),"metal_dark",.018)
    for xx in (x-w*.5,x+w*.5):
        box(name+" terrace parapet",(xx,y,h+1.0),(.25,d+.48,.65),"metal_dark",.018)
    # Solar slats and planter boxes make the exposed deck read as occupied.
    for i in range(4):
        xx=x-w*.36+i*w*.24
        box(name+" roof solar slat",(xx,y+d*.24,h+1.03),(w*.16,d*.24,.09),"metal_dark",.01,rot=(.07,-.08,0))
    for xx in (x-w*.38,x+w*.38):
        box(name+" terrace planter",(xx,y-d*.18,h+1.02),(3.0,1.0,.58),"stone",.05)
        alien_shrub(name+" terrace lichen",xx,y-d*.18,h+1.31,.50)


def solar_canopy(name, x, y, z, w, d, rows=1):
    for row in range(rows):
        yy = y + row * d / rows
        for i in range(3):
            xx = x - w * .34 + i * w * .34
            box(name + " photovoltaic laminate", (xx, yy, z), (w * .28, d / rows * .76, .11),
                "metal_dark", .012, rot=(.10, -.10, 0))
        for xx in (x - w * .42, x + w * .42):
            cylinder(name + " rack upright", (xx, yy, z * .5), .07, z, "metal", 8, .006)


# Keep the existing construction-call interface while changing the plant
# language to native, low-chroma fern canopies and ground lichens.
def tree(name, x, y, z=0, height=8, crown=3.2, conifer=False):
    alien_tree(name, x, y, z, height, crown, upright=conifer)


def shrub(name, x, y, z=0, r=1.3):
    alien_shrub(name, x, y, z, r)


def build_building(key):
    """Original Laisheng settlement architecture for all 21 upgrade nodes."""
    if key == "homestead":
        habitat_module("Homestead pressure cabin", -5, 1, 10, 7.6, 4.2, 3)
        habitat_module("Homestead family cabin", 5.2, 2.4, 8.0, 6.8, 3.9, 3, "terracotta")
        # Open utility runs and replaceable hardware are intentionally visible.
        box("Sheltered cabin link", (0, -3.5, 2.7), (8.0, 1.25, .22), "metal", .018)
        for x in (-3, 3):
            cylinder("Cabin link pier", (x, -3.5, 1.45), .07, 2.8, "metal_dark", 8, .007)
        service_cassette("Homestead heat and water cassette", -13, -1.5, scale=.88)
        cylinder("Rainwater buffer tank", (11, 5.5, 3.2), 1.35, 6.4, "metal_light", 16, .04)
        for z in (.9, 5.5):
            cylinder("Tank retaining band", (11, 5.5, z), 1.42, .16, "metal_dark", 16, .012)
        solar_canopy("Homestead solar trellis", -5, 7.8, 6.0, 10.5, 4.4, 1)
        for x in (-10, 1, 8):
            alien_shrub("Kitchen lichen bed", x, 8.5, .10, .9)
        for x in (-3, 0, 3):
            box("Food cultivation trough", (x, 8.0, .20), (1.45, 2.2, .28), "stone", .025)
    elif key == "neighborhood":
        habitat_module("Commons accommodation wing", -6.5, 1, 12.5, 8.4, 4.7, 4)
        habitat_module("Commons clinic and kitchen wing", 7.0, 1.5, 11.0, 8.0, 4.4, 4, "terracotta")
        box("Covered common walk", (0, -3.8, 3.65), (15, 2.4, .20), "metal_dark", .02)
        for x in (-6, -2, 2, 6):
            cylinder("Walkway light support", (x, -4.3, 1.7), .075, 3.4, "metal", 8, .006)
        box("Shared service court", (0, -8.2, .12), (21, 4.0, .24), "stone", .025)
        service_cassette("Commons water and air plant", -14, 5, scale=.92)
        for x in (-8, 8):
            alien_shrub("Commons lichen garden", x, -8.5, .16, 1.1)
        alien_tree("Commons shade fern", 12, 8, height=7.8, crown=2.5)
        box("Community comms mast foot", (-12, -8, .65), (1.3, 1.3, 1.3), "metal_dark", .03)
        cylinder("Community low-band mast", (-12, -8, 5.0), .065, 8.2, "metal", 8, .006)
    elif key == "workshop":
        equipment_hall("Open colony repair shop", 0, 1, 21, 14, 5.6, doors=3)
        for x in (-5.5, 0, 5.5):
            cylinder("Workshop roof exhaust fan", (x, 2.0, 6.65), .72, .55, "metal_dark", 12, .04)
            cylinder("Workshop fan cowl", (x, 2.0, 7.03), .86, .16, "metal", 12, .02)
        habitat_module("Workshop operator pod", -13.5, 1.8, 6.2, 6.0, 3.6, 2)
        service_cassette("Workshop power and heat exchange", 13.2, -2.0, scale=.92)
        # Accessible bins, pipe spools and an exterior service frame distinguish it from housing.
        for x in (-8.0, -5.3, -2.6):
            box("Sorted repair stock bin", (x, -9.0, 1.0), (2.3, 1.8, 2.0), "metal_dark", .035)
            box("Bin identification plate", (x, -9.95, 1.1), (1.2, .08, .62), "metal_light", .015)
        for i in range(3):
            cylinder("Workshop pipe spool", (9.0 + i * 1.35, -9.3, 1.05), .62, .30, "metal", 16, .025, (math.pi / 2, 0, 0))
        for x in (-11, 11):
            cylinder("Shop perimeter bollard", (x, -11.5, .7), .13, 1.4, "metal_dark", 10, .012)
    elif key == "residence":
        streetfront_block("Residence west court wing", -11.0, 2.1, 11.2, 14, 2, False)
        streetfront_block("Residence central street wing", 0, 1.5, 12.2, 15, 3, False, True)
        streetfront_block("Residence east street wing", 11.7, 2.1, 11.2, 14, 2, False)
        box("Shared planted court", (0, 9.2, .14), (23, 5.2, .28), "green", .025)
        for x in (-8, -3, 3, 8):
            alien_shrub("Residence court lichen", x, 9.2, .28, .75)
        alien_tree("Residence court fern", 0, 10.2, .28, 6.8, 2.2)
        box("Continuous residential sidewalk", (0, -7.2, .12), (35, 3.4, .24), "stone", .02)
        for x in (-14, 14):
            box("Street drain channel", (x, -8.0, .27), (.20, 1.9, .08), "metal_dark", .012)
    elif key == "agriculture":
        habitat_module("Agronomy operator cabin", -12.0, 1, 7.2, 6.4, 3.8, 2)
        greenhouse("Laisheng crop house A", 5.5, 1, 15.0, 11.0, 5.5)
        greenhouse("Laisheng crop house B", 1.5, 12.3, 12.0, 7.6, 4.7)
        service_cassette("Nutrient mixing cassette", -17, 8, scale=.82)
        cylinder("Closed-loop irrigation tank", (14.0, 9.8, 2.9), 1.0, 5.8, "metal_light", 16, .035)
        for row in range(3):
            for col in range(3):
                x, y = 11.2 + col * 1.35, -9.0 + row * 2.15
                box("Open cultivation channel", (x, y, .20), (.95, 1.8, .30), "stone", .025)
                for j in range(3):
                    alien_shrub("Crop lichen and fern", x - .3 + j * .3, y, .35, .35)
        for x in (-17, 17):
            alien_shrub("Field windbreak", x, -10, .1, 1.1)
    elif key == "public_center":
        streetfront_block("Public market west arcade", -11.0, 1.8, 11.4, 13, 2, True)
        streetfront_block("Public market hall", .1, 1.8, 12.0, 13, 2, True, True, "terracotta")
        streetfront_block("Public market east arcade", 11.8, 1.8, 11.3, 13, 2, True)
        box("Market forecourt", (0, -8.0, .12), (35, 5.2, .24), "stone", .022)
        for i, x in enumerate((-11, -4, 4, 11)):
            box("Market produce kiosk", (x, -9.0, 1.1), (3.6, 1.8, 2.2), "metal_dark", .035)
            box("Market kiosk canopy", (x, -9.0, 2.34), (4.0, 2.5, .14), "terracotta" if i % 2 == 0 else "metal", .018)
        alien_tree("Market square fern", 15, -8, .1, 7.5, 2.5)
    elif key == "medical_clinic":
        streetfront_block("Community clinic street wing", -9.0, 1.8, 12.0, 14, 2, False)
        streetfront_block("Community clinic entry wing", 4.8, 1.8, 13.5, 14, 3, False, True)
        box("Covered clinic arrival", (1.7, -8.1, 5.0), (12.0, 2.7, .26), "metal", .025)
        for x in (-2.4, 5.8):
            cylinder("Clinic canopy column", (x, -8.2, 2.5), .11, 5.0, "metal_dark", 10, .01)
        box("Care service sign panel", (-5.4, -5.55, 6.5), (2.5, .16, 1.0), "glass", .035)
        service_cassette("Clinic oxygen and power plant", 15.5, 8, scale=.76)
        for x in (-14, 14):
            alien_shrub("Clinic entry garden", x, -8.5, .14, 1.0)
    elif key == "primary_school":
        streetfront_block("Primary school east classroom", -8.4, 2.8, 16.0, 12.0, 2, False)
        streetfront_block("Primary school west classroom", 8.0, 2.8, 16.0, 12.0, 2, False)
        box("Sheltered school entry walk", (0, -4.5, 3.7), (14.5, 1.5, .20), "metal_dark", .02)
        for x in (-5.5, 0, 5.5):
            cylinder("School walk support", (x, -4.7, 1.8), .07, 3.6, "metal", 8, .006)
        box("Learning garden court", (0, -10.5, .12), (19.0, 6.0, .24), "green", .02)
        for x in (-7.5, -3.5, 3.5, 7.5):
            alien_shrub("School lichen bed", x, -10.5, .26, .68)
        alien_tree("School courtyard fern", 0, 7.7, .1, 7.2, 2.5)
    elif key == "power_station":
        equipment_hall("Block utility station", -2.5, 1.0, 18, 14, 8.0, doors=2)
        streetfront_block("Utility operator frontage", -13.0, 1.5, 7.0, 12, 2, False)
        for x in (9, 13):
            cylinder("Grid storage vessel", (x, 4.0, 3.4), 1.45, 6.8, "metal_light", 16, .04)
            for z in (1.2, 5.6):
                cylinder("Vessel access band", (x, 4.0, z), 1.52, .13, "metal_dark", 16, .012)
            box("Power junction panel", (x, -6.6, 3.0), (2.2, .18, 2.4), "metal_dark", .025)
        service_cassette("District energy balancing pod", -14, -7, scale=.78)
        for i in range(4):
            cylinder("Utility insulated header", (-1 + i * 2.4, -8.5, 2.1 + (i % 2) * .48), .12, 12.0,
                     "metal", 10, .012, (math.pi / 2, 0, math.pi / 2))
        for x in (-16, 16):
            cylinder("Service yard mast", (x, -12, 2.6), .20, 5.2, "metal", 10, .015)
    elif key == "extraction":
        equipment_hall("Subsurface materials sorting hall", -1.0, 2.0, 13.5, 11.5, 6.2, doors=2)
        silo("Dust recovery and extraction stack", 10.5, 2.5, 0, .92, 10.8, "metal_dark")
        for z in (3.0, 7.8):
            cylinder("Stack service hoop", (10.5, 2.5, z), 1.08, .16, "ochre", 16, .012)
        box("Elevated extraction duct", (4.8, 2.8, 8.2), (10.0, .55, .42), "metal", .035)
        for x in (1.2, 8.0):
            cylinder("Extraction duct trestle", (x, 2.8, 4.2), .10, 7.6, "metal_dark", 8, .008)
        for x, y, r, h in ((-12, 5, 2.0, 3.2), (-9, 7, 1.6, 2.5), (11, 7, 2.1, 3.5), (14, 4, 1.6, 2.8)):
            rock("Laisheng cut-stone stock", x, y, 0, r, "stone_dark" if h > 3 else "stone")
        box("Ore intake hopper", (-1.0, -6.8, 2.6), (5.8, 2.8, 3.8), "metal_dark", .07)
        mesh("Tapered intake chute", [(-3.8, -8.25, 4.2), (1.8, -8.25, 4.2), (.9, -8.25, 1.0), (-2.9, -8.25, 1.0)],
             [(0, 1, 2, 3)], "metal_light")
        for x in (-8, -5, -2):
            cylinder("Covered transfer conveyor", (x, -10.0, 1.8), .13, 7.0, "metal", 8, .01, (0, .18, 0))
        service_cassette("Extraction wash-water reclaim", 11.5, -7.5, scale=.76)
        for x in (-15, 15):
            alien_shrub("Cut face lichen", x, -9.5, .08, .82)
    elif key == "yard":
        equipment_hall("Materials depot", -7.0, 2.0, 15, 13, 7.2, doors=2)
        habitat_module("Depot dispatch pod", -17, 2.0, 5.7, 5.8, 3.4, 2)
        # Neatly sorted containers stay low so the yard remains legible at scale.
        for x in (4.0, 9.0, 14.0):
            for y in (-5.0, .0, 5.0):
                box("Reusable cargo cassette", (x, y, 1.25), (4.5, 2.4, 2.5), "metal", .04)
                for xx in (-1.55, 1.55):
                    box("Cassette corner post", (x + xx, y - 1.23, 1.25), (.10, .08, 2.2), "metal_light", .008)
        service_cassette("Depot charging and air pod", 14.5, -9.5, scale=.72)
        box("Cargo lane paving", (0, -10.0, .12), (34, 3.0, .24), "stone", .02)
    elif key == "advanced_residence":
        # Joined mid-rise street walls and one narrow stair core make a dense
        # neighborhood with a single taller landmark instead of isolated towers.
        streetfront_block("Laisheng midrise west block", -12.2, 2.0, 12.5, 16, 4, False)
        streetfront_block("Laisheng central housing core", .0, 1.5, 13.0, 16, 6, False, True)
        streetfront_block("Laisheng midrise east block", 12.4, 2.0, 12.5, 16, 4, False)
        streetfront_block("Laisheng rear courtyard wing", 0, 12.6, 19.0, 10, 4, False)
        box("Connected planted urban court", (0, 9.0, .16), (18.5, 5.4, .32), "green", .02)
        for x in (-7, -3, 3, 7):
            alien_shrub("Roof-water garden court", x, 9.1, .34, .78)
        alien_tree("Court canopy fern", 0, 9.8, .32, 8.0, 2.8)
        box("Continuous pedestrian eave", (0, -7.0, 3.3), (37, 1.15, .18), "metal", .018)
    elif key == "modern_agriculture":
        greenhouse("Automated grow hall west", -10, 1, 19, 14, 7.0)
        greenhouse("Automated grow hall east", 10, 1, 19, 14, 7.0)
        greenhouse("Seed and nursery span", 0, 13.5, 15.5, 8.5, 5.4)
        streetfront_block("Vertical crop and control core", 0, 0, 10.5, 12, 4, False, True)
        service_cassette("Automated nutrient loop", -18.0, 11.0, scale=.86)
        for x in (-17, 17):
            cylinder("Nutrient buffer vessel", (x, 12, 2.5), 1.05, 5.0, "metal_light", 16, .03)
        solar_canopy("Grow hall energy rack", 0, -7.8, 9.2, 27, 4.8, 2)
        for row in range(3):
            for col in range(4):
                x, y = 11 + col * 1.3, -10 + row * 2.0
                box("Outdoor closed nutrient bed", (x, y, .19), (.92, 1.65, .28), "stone", .022)
                alien_shrub("Nutrient bed vegetation", x, y, .31, .38)
    elif key == "commercial_center":
        # A civic retail plinth spans the block while the narrower office core
        # and low shop wings rise above it, distinct from the residential slabs.
        retail_podium("Laisheng commercial arcade", 0.0, 1.2, 38.0, 17.0, 4.35)
        streetfront_block("Commercial central office core", 0.0, 1.2, 14.5, 17, 6, True, True, "terracotta")
        streetfront_block("Commercial west shop wing", -13.1, 1.8, 11.0, 15, 4, True)
        streetfront_block("Commercial east shop wing", 13.1, 1.8, 11.0, 15, 4, True)
        box("Continuous urban sidewalk", (0, -8.5, .12), (39, 4.6, .24), "stone", .02)
        alien_tree("Commercial plaza fern", 17, -10, .12, 8.0, 2.8)
        for x in (-8, 8):
            box("Street utility cabinet", (x, -10.0, .75), (1.3, .7, 1.5), "metal_dark", .025)
    elif key == "garden":
        box("Laisheng commons lawn", (0, 0, .10), (33, 27, .20), "green", .025)
        for x, y, rot, length in ((0, -9, 0, 26), (0, 9, 0, 26), (-13, 0, math.pi / 2, 18), (13, 0, math.pi / 2, 18)):
            path_segment("Mineral garden promenade", x, y, .22, length, 1.8, "stone", rot)
        cylinder("Reclaimed-water reflecting pool", (0, 1, .34), 4.0, .34, "water", 24, .035)
        cylinder("Pool edge course", (0, 1, .22), 4.35, .16, "stone_dark", 24, .02)
        streetfront_block("Garden research kiosk", 0, -5, 7.5, 6.5, 1, False)
        for x, y, h, r in ((-11, -7, 10, 3.0), (12, -7, 9, 2.7), (-11, 8, 9, 2.8), (12, 8, 10, 3.1), (-3, 11, 8, 2.4)):
            alien_tree("Mature colony fern", x, y, height=h, crown=r)
        for x, y in ((-6, -9), (6, -9), (-12, 2), (12, 2)):
            bench("Recycled alloy garden seat", x, y, 0)
        for i in range(10):
            angle = i * math.tau / 10
            alien_shrub("Lichen-ring planter", 7.2 * math.cos(angle), 7.2 * math.sin(angle), .18, .7)
    elif key == "hospital":
        # Patient and diagnostic wards stay low; one compact clinical core,
        # glazed entry atrium and marked drop-off identify the hospital.
        streetfront_block("Hospital long west patient wing", -12.5, 1.7, 15.5, 17, 3, False)
        streetfront_block("Hospital central clinical core", 0.0, 2.1, 13.5, 15, 5, False, True)
        streetfront_block("Hospital long east diagnostic wing", 12.5, 1.7, 15.5, 17, 3, False)
        box("Hospital glazed arrival atrium", (0, -5.67, 4.45), (6.4, .18, 6.8), "glass_light", .025)
        for x in (-1.9, 0, 1.9):
            box("Atrium bronze mullion", (x, -5.80, 4.45), (.11, .08, 6.6), "metal", .012)
        box("Clinical sign field", (0, -5.92, 13.0), (2.2, .18, 2.2), "cream_sign", .10)
        box("Clinical sign vertical glyph", (0, -6.05, 13.0), (.42, .12, 1.58), "water", .06)
        box("Clinical sign horizontal glyph", (0, -6.06, 13.0), (1.58, .12, .42), "water", .06)
        box("Hospital sheltered arrival", (0, -8.8, 5.2), (16.5, 3.2, .28), "metal", .025)
        for x in (-6.0, -2.0, 2.0, 6.0):
            cylinder("Arrival canopy support", (x, -8.9, 2.6), .10, 5.2, "metal_dark", 10, .008)
        service_cassette("Clinical oxygen service pod", -18.0, 9.8, scale=.75)
        box("Healing garden forecourt", (0, -13.2, .11), (28, 3.2, .22), "stone", .018)
        for x in (-14, -8, 8, 14):
            alien_shrub("Healing court lichen", x, -12.9, .22, .76)
        alien_tree("Healing court fern", 0, 11.0, .12, 8.2, 2.6)
    elif key == "secondary_school":
        # An open-fronted U-shaped campus encloses a learning court instead
        # of reading as another three-piece residential street wall.
        streetfront_block("Secondary school west classroom bar", -12.0, 2.0, 10.0, 22.0, 2, False)
        streetfront_block("Secondary school east classroom bar", 12.0, 2.0, 10.0, 22.0, 2, False)
        box("Secondary school assembly hall", (0, 11.0, 3.9), (14.0, 7.0, 7.4), "cream", .025)
        front_windows("Assembly hall glazed entry", 0, 7.5, .7, 11.4, 5.9, 6, 1)
        side_windows("Assembly hall return windows", 7.0, 11.0, .7, 7.0, 5.9, 3, 1)
        box("Assembly hall roof slab", (0, 11.0, 7.75), (14.5, 7.5, .32), "metal_dark", .025)
        sawtooth_roof("Science hall daylight monitors", 0, 11.0, 7.98, 13.8, 6.8, 5)
        box("Open campus learning court", (0, -1.0, .12), (13.2, 14.5, .24), "green", .02)
        box("Court mineral running lane", (0, -1.0, .27), (10.4, .10, .06), "terracotta", .01)
        box("Campus entrance eave", (0, -8.6, 3.3), (13.6, 1.5, .20), "metal_dark", .018)
        for x in (-6.0, -2.0, 2.0, 6.0):
            cylinder("Campus entrance support", (x, -8.6, 1.65), .075, 3.3, "metal", 8, .006)
        for x in (-5.5, 5.5):
            alien_shrub("School courtyard planting", x, 1.0, .25, .70)
        alien_tree("School court fern", 0, -1.0, .10, 7.4, 2.0)
    elif key == "power_plant":
        equipment_hall("Regional turbine hall", -4.5, 1, 25, 18, 12.5, doors=3)
        streetfront_block("Plant control and dispatch frontage", -17.5, 1.4, 8.0, 14, 3, False)
        thermal_tower("Primary closed-loop cooling tower", 10, 2, 0, 3.2, 1.85, 12.0)
        thermal_tower("Secondary heat rejection tower", 16.5, 5, 0, 2.5, 1.55, 10.0)
        for x in (-10.5, -3.5, 3.5):
            rooftop_turbine("Turbine hall roof ventilator", x, 3.2, 13.4, 2.0)
        for x in (-15, 1):
            cylinder("Filtered vent stack", (x, 8, 8.2), .68, 16.4, "metal_dark", 14, .035)
            cylinder("Vent stack rain cap", (x, 8, 16.6), .86, .20, "metal", 14, .02)
        service_cassette("Plant load balancing equipment", 12.0, -9.0, scale=.78)
        box("Transformer skid", (11, -7.2, 1.5), (5.4, 3.2, 3.0), "metal_dark", .04)
        for z in (1.4, 2.0, 2.6):
            box("Transformer cooling rib", (11, -8.85, z), (3.8, .08, .12), "metal_light", .008)
    elif key == "material_factory":
        equipment_hall("Regolith and alloy processing hall", -5.0, 1.0, 24, 18, 10.2, doors=3, roof_style="sawtooth")
        streetfront_block("Materials process control block", -17.5, 1.6, 7.5, 14, 3, False)
        for x, radius, height in ((9.7, 2.0, 11.2), (14.4, 2.1, 12.4), (19.0, 1.65, 9.8)):
            silo("Regolith powder containment silo", x, 5.0, 0, radius, height, "metal_light")
            for z in (1.0, height - 1.3):
                cylinder("Silo expansion band", (x, 5.0, z), radius + .08, .14, "metal_dark", 18, .012)
        # A raised enclosed belt feeds the silos without repeating the power
        # plant's towers and vents.
        box("Elevated alloy transfer conveyor", (14.0, 2.2, 9.4), (12.8, .64, .44), "metal_dark", .035)
        for x in (9.0, 18.8):
            cylinder("Elevated conveyor trestle", (x, 2.2, 4.8), .13, 9.0, "metal", 10, .012)
        for x in (11.8, 16.2):
            box("Conveyor safety gate", (x, 1.84, 8.8), (.14, .08, 1.05), "ochre", .015)
        for x in (3, 7):
            box("Covered aggregate transfer belt", (x, -4.5, 4.0), (5.6, .38, .26), "metal_dark", .02, rot=(0, .18, 0))
            for dx in (-2.2, 2.2):
                cylinder("Belt trestle", (x + dx, -4.5, 2.0), .09, 4.0, "metal", 8, .006)
        service_cassette("Process water recovery", 16.0, -8.0, scale=.74)
    elif key == "manufacturing":
        equipment_hall("Precision assembly hall", -3.0, 1.0, 28, 19, 11.5, doors=3, roof_style="sawtooth")
        # Repeated glazed monitor lights break the roof into work bays and
        # give the assembly hall a recognizable industrial sawtooth profile.
        sawtooth_roof("Precision hall northlight roof", -3.0, 1.0, 12.25, 27.0, 17.8, 6)
        streetfront_block("Manufacturing office street wing", -17.3, 1.4, 8.5, 15, 3, False)
        for x in (-12, -5, 2):
            box("Recessed loading bay", (x, -9.0, 2.6), (4.3, .18, 4.8), "metal_dark", .03)
            box("Sectional loading door", (x, -9.14, 2.6), (3.8, .07, 4.35), "metal", .018)
            for z in (1.1, 2.0, 2.9, 3.8):
                box("Door horizontal rib", (x, -9.20, z), (3.5, .04, .06), "metal_light", .006)
        for x in (10, 15):
            box("Protected freight platform", (x, -12.2, .42), (4.8, 3.0, .84), "stone_dark", .025)
        service_cassette("Manufacturing coolant and power pod", 15.0, -6.7, scale=.76)
    elif key == "freight":
        equipment_hall("Automated freight hall", -3.0, 2.8, 31, 18, 11.2, doors=4)
        streetfront_block("Freight operations frontage", -18.5, 2.2, 7.5, 14, 3, False)
        # A restrained handling gantry gives the terminal its identity.
        for x in (-14.0, 14.0):
            box("Cargo gantry leg", (x, -10.2, 3.8), (.48, .60, 7.6), "metal_dark", .025)
            box("Gantry foot", (x, -10.2, .30), (1.8, 2.2, .60), "metal", .025)
        box("Cargo gantry beam", (0, -10.2, 7.55), (29, .68, .72), "metal", .035)
        box("Gantry service carriage", (3, -10.2, 6.9), (3.5, 1.15, .50), "metal_light", .03)
        cylinder("Gantry lift cable", (3, -10.2, 5.5), .035, 2.8, "metal_dark", 8, .004)
        for i, color in enumerate(("metal_dark", "terracotta", "metal")):
            x = -12 + i * 10
            box("Reusable sealed cargo cassette", (x, -15.6, 1.55), (7.8, 3.8, 3.1), color, .035)
            for xx in range(4):
                box("Cassette side rib", (x - 2.7 + xx * 1.8, -17.55, 1.55), (.075, .05, 2.65), "metal_light", .006)
        service_cassette("Freight station charging pod", 17.0, -7.5, scale=.74)
    else:
        raise KeyError("No building design for " + key)

def bench(name,x,y,z=0):
    box(name+" seat",(x,y,z+.62),(2.7,.62,.18),"wood_light",.04)
    box(name+" back",(x,y+.27,z+1.18),(2.7,.15,.94),"wood",.035)
    for xx in (-1,1):
        for yy in (-.20,.20): box(name+" support",(x+xx,y+yy,z+.34),(.14,.14,.68),"metal_dark",.02)


def build_prop(key):
    if key=="tree_broadleaf": tree("Broadleaf park tree",0,0,height=13,crown=4.4)
    elif key=="tree_conifer": tree("Narrow evergreen",0,0,height=13.5,crown=3.7,conifer=True)
    elif key=="shrub": shrub("Round ornamental shrub",0,0,0,2.0)
    elif key=="rocks":
        rock("Granite boulder",-1.1,0,0,1.65,"stone_dark")
        rock("Weathered boulder",1.1,.45,0,1.25,"stone")
        rock("Low stone",.55,-1.15,0,.83,"sand")
        for i in range(3): ico("Lichen patch",(-.5+i*.55,-.55,.82),(.18,.14,.09),"green",1)
    elif key=="bench": bench("Slatted town bench",0,0,0)
    elif key=="lamp":
        cylinder("Lamp post",(0,0,5.4),.13,10.8,"metal_dark",10,.02)
        cylinder("Lamp base",(0,0,.36),.43,.72,"stone",12,.04)
        box("Lamp cross arm",(0,0,10.7),(1.5,.15,.15),"metal",.025)
        cylinder("Lantern frame",(.60,0,10.25),.52,.95,"metal_dark",8,.04)
        ico("Lantern warm glass",(.60,-.06,10.25),(.39,.36,.50),"cream_sign",1)
        box("Lantern cap",(.60,0,10.83),(1.05,.95,.22),"metal",.05)
    elif key=="fence":
        fence_run("Garden fence section",0,0,0,12,axis="x",posts=5)
        for x in (-6,6):
            ico("Fence post cap",(x,0,2.08),(.20,.20,.16),"gold",1)
    elif key=="stall":
        box("Timber stall counter",(0,0,1.30),(8,3,2.6),"wood",.10)
        box("Counter front panel",(0,-1.56,1.30),(7.4,.12,1.75),"wood_light",.035)
        for x in (-3.5,3.5): cylinder("Stall canopy pole",(x,0,4.1),.11,3.0,"metal_dark",10,.02)
        box("Striped market canopy",(0,0,5.65),(9.2,4.2,.30),"terracotta",.12)
        for x in (-3,-1,1,3): box("Canopy seam",(x,0,5.84),(.09,4,.05),"cream_sign",.01)
        for x in (-2,0,2):
            ico("Fresh produce",(x,-.75,2.85),(.55,.45,.45),"crop",1)
    elif key=="crates":
        for x,y,z,w,d,h in ((-2.4,-.5,1.0,2.0,1.8,2.0),(.0,-.5,1.0,2.0,1.8,2.0),
                            (2.4,-.5,1.0,2.0,1.8,2.0),(-1.2,-.5,3.1,2.0,1.8,2.0),(1.2,-.5,3.1,2.0,1.8,2.0)):
            box("Shipping crate",(x,y,z),(w,d,h),"wood_light",.09)
            for xx in (-.7,.7): box("Crate batten",(x+xx,y-.95,z),(0.14,.10,h*.9),"wood",.02)
            box("Crate lid seam",(x,y-.98,z+h*.20),(w*.78,.08,.08),"wood",.01)
    elif key=="materials":
        for i in range(3):
            box("Timber beam stack",(-2.4+i*2.4,0,1.1),(2.0,5.4,2.0),"wood_light",.07)
            for z in (.35,.95,1.55): box("Beam layer",(-2.4+i*2.4,0,z),(2.2,5.6,.14),"wood",.025)
        for x in (-3.7,3.7): rock("Stone block",x,-3,0,1.0,"stone_dark")
    elif key=="furrows":
        box("Furrow soil bed",(0,0,.15),(12,10.5,.30),"soil",.06)
        for x in (-4,-2,0,2,4):
            box("Raised crop ridge",(x,0,.42),(.42,8.5,.26),"soil_light",.12)
            for y in range(-3,4,2):
                ico("Young crop",(x,y,.95),(.20,.35,.54),"green_light",1)
                ico("Young crop leaf",(x+.25,y+.14,.78),(.26,.17,.30),"green",1)
    elif key=="flowers":
        box("Wildflower patch",(0,0,.13),(11,9,.26),"green",.06)
        colors=("gold","terracotta","cream_sign","water")
        for i in range(12):
            a=i*math.tau/12
            x=4.2*math.cos(a);y=3.1*math.sin(a)
            cylinder("Flower stem",(x,y,.66),.045,.9,"green_dark",7,.005)
            ico("Flower head",(x,y,1.15),(.29,.27,.20),colors[i%4],1)
    elif key=="storage":
        wall("Modular storage shed",0,0,13,10,6.5,"cream","metal","gable")
        door("Storage roller door",0,-5.4,0,3.4,4.0,"metal_dark")
        for x in (-5,5): box("Storage loading marker",(x,-6,.8),(1.1,.55,1.6),"gold",.05)
    elif key=="heat":
        for x in (-3,0,3): silo("Heat exchange vessel",x,0,0,1.25,8,"metal_light")
        for z in (3,5.7):
            box("District heating header",(0,-1.6,z),(10,.28,.28),"metal_dark",.04)
            for x in (-3,0,3): cylinder("Pipe elbow",(x,-1.6,z-.65),.18,1.4,"metal",10,.02)
        for x in (-5,5): cylinder("Insulated pipe",(x,0,0),.35,8,"metal_dark",10,.04,(math.pi/2,0,0))
    elif key=="recycle":
        for x,c in ((-4,"green"),(0,"metal"),(4,"water")):
            box("Recycling container",(x,0,2.2),(3.3,4.2,4.4),c,.12)
            box("Recycling lid",(x,0,4.55),(3.6,4.5,.30),"metal_dark",.06)
            box("Container label plate",(x,-2.16,2.8),(1.45,.10,1.0),"cream_sign",.06)
        for x in (-5,5): ico("Reclaimed material",(x,-3,1.0),(1.3,.8,1.1),"wood_light",1)
    elif key=="open_court":
        box("Community open court paving",(0,0,.12),(17,15,.24),"stone",.06)
        for x in (-5.5,0,5.5):
            box("Court planter",(x,4,.52),(3.5,2.6,.8),"sand",.08)
            shrub("Court planted box",x,4,.9,1.0)
        for x in (-6,6):
            cylinder("Pergola post",(x,-3.8,2.5),.12,5,"wood",10,.02)
        for x in (-6,-2,2,6): box("Pergola beam",(x,-3.8,5),(1.2,8,.2),"wood_light",.03)
        bench("Public courtyard seat",0,-6,0)
    elif key=="reuse":
        # Small conservation pavilion and re-used brick wall fragment.
        for x in (-5,5): box("Reuse pavilion pier",(x,0,3.2),(1.0,6.2,6.4),"red_brick",.08)
        box("Reuse pavilion beam",(0,0,6.3),(11.3,6.4,.6),"wood_light",.08)
        gable_roof("Reuse canopy",0,0,6.55,11,6.2,2.8,"terracotta")
        for x in (-3.4,-1.7,0,1.7,3.4):
            box("Reclaimed timber screen",(x,-2.9,3.0),(.28,.2,5.7),"wood",.035)
        box("Historic brick remnant",(0,3.7,2.0),(8,.55,4.0),"red_brick",.08)
        for x in (-2.5,2.5): box("Masonry repair patch",(x,3.35,2.5),(1.7,.12,2.4),"sand",.03)
    elif key=="roof_farm":
        box("Urban roof garden deck",(0,0,.23),(16,13,.46),"stone",.08)
        for x in (-5,0,5):
            box("Raised planting trough",(x,0,1.2),(3.3,10,1.5),"sand",.10)
            for y in range(-4,5,2):
                for xx in (-.8,0,.8): ico("Roof garden crop",(x+xx,y,2.25),(.24,.40,.55),"green_light",1)
        for x in (-7,7): box("Garden solar rail",(x,0,1.7),(.18,11,2.3),"metal_dark",.025)
    elif key=="drone":
        box("Drone station landing pad",(0,0,.18),(17,15,.36),"metal_light",.10)
        box("Pad boundary",(0,0,.40),(14,12,.10),"metal_dark",.02)
        box("Pad central marking",(0,0,.48),(8,.22,.035),"cream_sign",.01)
        box("Landing marking crossbar",(0,0,.48),(.22,8,.035),"cream_sign",.01)
        # Parked quadcopter model, built with visible arms and rotor guards.
        box("Drone central fuselage",(0,0,2.0),(2.2,1.7,.75),"cream",.16)
        ico("Drone sensor",(0,-.82,1.95),(.40,.18,.28),"water",1)
        for x in (-3.6,3.6):
            for y in (-2.3,2.3):
                box("Drone carbon arm",(x*.55,y*.52,2.0),(4.2,.19,.20),"metal_dark",.06,(0,0,math.atan2(y,x)))
                cylinder("Drone motor",(x,y,2.25),.32,.48,"metal",12,.03)
                cylinder("Drone rotor disk",(x,y,2.54),1.25,.06,"metal_light",20,.02)
    elif key=="workshop_module":
        box("Add-on workshop footing",(0,0,.20),(13,11,.4),"stone",.07)
        wall("Compact annex workshop",0,0,12,10,6.5,"sand","terracotta","gable")
        front_windows("Annex work windows",0,-5.2,0,11,5.8,3,1)
        door("Annex double door",0,-5.3,0,3.1,3.6,"wood")
        chimney("Workshop add-on flue",4,2.5,6.7,3.3)
    elif key in ("hill_0","hill_1","hill_2"):
        tier=int(key[-1])
        # Ground-contact low-poly ridges: broad rock feet, broken facets and
        # a pale stratified cap; these replace the old single pyramid hill.
        base=(11+4*tier,8+3*tier,5+3*tier)
        ico("Hill mass",(0,0,base[2]*.42),(base[0],base[1],base[2]),"stone",1 if tier<2 else 2)
        for i in range(3+tier):
            x=-base[0]*.65+i*(base[0]*1.3/(2+tier))
            y=-base[1]*.55+(i%2)*base[1]*.22
            rock("Broken foothill shoulder",x,y,0,1.8+tier*.55,"stone_dark" if i%2 else "sand")
        if tier>=1:
            ico("High ridge outcrop",(base[0]*.2,base[1]*.1,base[2]*.88),(base[0]*.58,base[1]*.40,base[2]*.64),"stone_dark",1)
        if tier>=2:
            ico("Sunlit alpine shelf",(-base[0]*.18,base[1]*.32,base[2]*1.20),(base[0]*.36,base[1]*.28,base[2]*.30),"sand",1)
        tree("Hill edge conifer",-base[0]*.72,-base[1]*.32,height=7,crown=2.1,conifer=True)
        shrub("Hill edge brush",base[0]*.70,-base[1]*.30,0,1.6)
    elif key=="garden_courtyard":
        box("Courtyard stone walk",(0,0,.10),(17,13,.20),"sand",.06)
        for x,y in ((-5,-3),(5,-3),(-5,3),(5,3)):
            box("Courtyard planting bed",(x,y,.43),(3.4,2.4,.66),"stone",.08)
            shrub("Courtyard clipped shrub",x,y,.78,1.0)
        cylinder("Courtyard fountain basin",(0,0,.45),2.0,.7,"stone",16,.07)
        cylinder("Courtyard water",(0,0,.84),1.42,.12,"water",16,.025)
        cylinder("Fountain stem",(0,0,1.6),.23,1.45,"stone_dark",12,.03)
        cylinder("Fountain bowl",(0,0,2.35),.76,.20,"stone",16,.04)
        for x in (-6,6): bench("Courtyard bench",x,0,0)
    else:
        raise KeyError("No prop design for " + key)


def camera_scale(kind,asset_id):
    if kind=="building": return ORTHO_WORLD_UNITS
    if asset_id in MODULE_ASSET_IDS: return MODULE_VIEW_UNITS
    if asset_id.startswith("hill_"): return TERRAIN_VIEW_UNITS
    if asset_id=="garden_courtyard": return COURTYARD_VIEW_UNITS
    return PROP_VIEW_UNITS


def configure_camera(scene,ortho_world_units):
    from mathutils import Vector
    cam_data=bpy.data.cameras.new("MET040 fixed oblique orthographic")
    cam=bpy.data.objects.new("MET040 camera",cam_data)
    scene.collection.objects.link(cam)
    direction=Vector(CAMERA_VECTOR).normalized()
    # The camera points from CAMERA_VECTOR toward origin. Shift the aim upward
    # in image space so world (0,0,0) stays at a stable, inspectable foot anchor.
    rotation=direction.to_track_quat("Z","Y")
    # to_track_quat tracks +Z; camera looks down its local -Z axis.
    rot=rotation.to_matrix()
    local_up=rot @ Vector((0,1,0))
    look=local_up*((.5-ANCHOR_NDC_Y)*ortho_world_units)
    target=Vector((0,0,0))+look
    cam.location=target+direction*64
    cam.rotation_euler=(target-cam.location).to_track_quat("-Z","Y").to_euler()
    cam.data.type="ORTHO"
    cam.data.ortho_scale=ortho_world_units
    cam.data.lens=50
    scene.camera=cam
    return cam


def configure_scene(size,kind,asset_id):
    scene=bpy.context.scene
    scene.render.engine="BLENDER_EEVEE_NEXT"
    scene.render.resolution_x=size
    scene.render.resolution_y=size
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.image_settings.color_depth="8"
    scene.render.film_transparent=True
    scene.render.image_settings.compression=15
    scene.render.resolution_percentage=100
    scene.render.filepath="/tmp/met040_asset.png"
    scene.render.film_transparent=True
    scene.view_settings.view_transform="Standard"
    scene.view_settings.look="None"
    scene.view_settings.exposure=0.28
    scene.view_settings.gamma=1
    scene.render.image_settings.color_mode="RGBA"
    scene.world.color=(.20,.20,.20)
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(0.48,0.49,0.47,1)
    bg.inputs["Strength"].default_value=.58
    scene.render.resolution_percentage=100
    scene.eevee.taa_render_samples=32
    scene.render.film_transparent=True
    ortho_world_units=camera_scale(kind,asset_id)
    cam=configure_camera(scene,ortho_world_units)
    from mathutils import Vector
    # Broad, warm daylight from screen upper-left. Area-light size softens edges.
    screen_left=cam.matrix_world.to_3x3() @ Vector((-1,0,0))
    screen_up=cam.matrix_world.to_3x3() @ Vector((0,1,0))
    direction=Vector(CAMERA_VECTOR).normalized()
    target=cam.location-direction*64
    loc=target+screen_left*24+screen_up*27+direction*30
    ld=bpy.data.lights.new("large soft upper-left daylight","AREA")
    lo=bpy.data.objects.new("large soft upper-left daylight",ld)
    scene.collection.objects.link(lo)
    lo.location=loc
    lo.rotation_euler=(target-lo.location).to_track_quat("-Z","Y").to_euler()
    ld.energy=5500
    ld.shape="DISK"
    ld.size=24
    ld.color=(1.0,.92,.80)
    # Soft frontal fill keeps warm plaster and blue-green metal distinct
    # without flattening the upper-left key light's roof and canopy shadows.
    fill_loc=target+screen_up*13+direction*35
    fill_data=bpy.data.lights.new("broad frontal fill","AREA")
    fill_obj=bpy.data.objects.new("broad frontal fill",fill_data)
    scene.collection.objects.link(fill_obj)
    fill_obj.location=fill_loc
    fill_obj.rotation_euler=(target-fill_obj.location).to_track_quat("-Z","Y").to_euler()
    fill_data.energy=2600
    fill_data.shape="DISK"
    fill_data.size=38
    # Add a broad warm sun direction for clearer roof and foliage planes.
    sun_data=bpy.data.lights.new("warm directional daylight","SUN")
    sun_obj=bpy.data.objects.new("warm directional daylight",sun_data)
    scene.collection.objects.link(sun_obj)
    sun_obj.location=target+screen_left*28+screen_up*34+direction*28
    sun_obj.rotation_euler=(target-sun_obj.location).to_track_quat("-Z","Y").to_euler()
    sun_data.energy=1.42
    sun_data.angle=math.radians(12)
    scene.render.film_transparent=True
    scene.render.image_settings.color_mode="RGBA"
    scene.render.image_settings.color_depth="8"
    # Gentle environment fill keeps shaded ivory walls legible.
    return scene,cam


def render_one(asset_id, kind, filename, size):
    clear_scene()
    if kind=="building": build_building(asset_id)
    else: build_prop(asset_id)
    scene,cam=configure_scene(size,kind,asset_id)
    # EEVEE Next's broad area light shades all mesh faces. There is no ground
    # plane, so the game can draw its one consistent soft tile shadow.
    out=Path(filename)
    out.parent.mkdir(parents=True,exist_ok=True)
    scene.render.filepath=str(out)
    bpy.ops.render.render(write_still=True)
    bpy.context.view_layer.update()
    from mathutils import Vector
    points=[]
    ground_points=[]
    roof_points=[]
    depsgraph=bpy.context.evaluated_depsgraph_get()
    for obj in scene.objects:
        if obj.type not in {"MESH","CURVE","FONT","SURFACE","META"}:
            continue
        evaluated=obj.evaluated_get(depsgraph)
        corners=[evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box]
        points.extend(corners)
        if corners and min(v.z for v in corners) <= .05:
            ground_points.extend(corners)
        obj_name=obj.name.lower()
        if corners and any(token in obj_name for token in ("roof", "eave", "parapet", "solar strip", "photovoltaic")):
            roof_points.extend(corners)
    world_bounds=[
        min(v[0] for v in points), min(v[1] for v in points), min(v[2] for v in points),
        max(v[0] for v in points), max(v[1] for v in points), max(v[2] for v in points),
    ] if points else [0,0,0,0,0,0]
    footprint_bounds=[
        min(v[0] for v in ground_points), min(v[1] for v in ground_points),
        max(v[0] for v in ground_points), max(v[1] for v in ground_points),
    ] if ground_points else [0,0,0,0]
    roof_bounds=[
        min(v[0] for v in roof_points), min(v[1] for v in roof_points), min(v[2] for v in roof_points),
        max(v[0] for v in roof_points), max(v[1] for v in roof_points), max(v[2] for v in roof_points),
    ] if roof_points else None
    return out, world_bounds, footprint_bounds, roof_bounds


def save_source_scene(asset_id):
    """Build one complete, editable Blender scene as a reproducible source."""
    clear_scene()
    build_building(asset_id)
    scene, _ = configure_scene(BUILDING_SIZE, "building", asset_id)
    scene.render.filepath = "//" + asset_id + "_preview.png"
    out = SOURCE / "source_scenes" / (asset_id + ".blend")
    out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    print("BLEND_SOURCE", out, "objects", len(scene.objects), flush=True)


def png_alpha_bounds(path):
    """Return top-left pixel bounds without relying on Pillow in Blender."""
    from array import array
    image=bpy.data.images.load(str(path), check_existing=False)
    width,height=map(int,image.size)
    pixels=array("f",[0.0])*(width*height*4)
    image.pixels.foreach_get(pixels)
    left,width_right=width,-1
    bottom,top=-1,height
    for offset in range(3,len(pixels),4):
        if pixels[offset] > 0.01:
            p=offset//4
            x=p%width
            y=p//width
            if x<left: left=x
            if x>width_right: width_right=x
            if y<top: top=y
            if y>bottom: bottom=y
    bpy.data.images.remove(image)
    if width_right<left or bottom<top:
        return [0,0,width,height]
    return [left,height-1-bottom,width_right+1,height-top]


BUILDINGS=[
    "homestead","neighborhood","workshop","residence","agriculture","public_center",
    "medical_clinic","primary_school","power_station","extraction","yard",
    "advanced_residence","modern_agriculture","commercial_center","garden","hospital",
    "secondary_school","power_plant","material_factory","manufacturing","freight",
]
TIERS={"homestead":1,"neighborhood":1,"workshop":1}
for _k in BUILDINGS:
    if _k not in TIERS:
        TIERS[_k]=2 if _k in {"residence","agriculture","public_center","medical_clinic","primary_school","power_station","extraction","yard"} else 3

PROPS=["tree_broadleaf","tree_conifer","shrub","rocks","bench","lamp","fence","stall","crates","materials","furrows","flowers",
       "storage","heat","recycle","open_court","reuse","roof_farm","drone","workshop_module",
       "hill_0","hill_1","hill_2","garden_courtyard"]
MODULE_ASSET_IDS={
    "storage":"module_storage","heat":"module_heat","recycle":"module_recycle",
    "open_court":"module_open_court","reuse":"module_reuse","roof_farm":"module_roof_farm",
    "drone":"module_drone","workshop_module":"module_workshop",
}

OLD_BUILDING_ALIASES={
    "hamlet":"building_homestead", "farm":"building_agriculture", "timber":"building_yard",
    "power":"building_power_station", "works":"building_workshop", "market":"building_public_center",
    "school":"building_primary_school", "clinic":"building_medical_clinic", "park":"building_garden",
    "warehouse":"building_freight",
}
OLD_MODULE_ALIASES={
    "storage":"module_storage","heat":"module_heat","recycle":"module_recycle",
    "open_court":"module_open_court","reuse":"module_reuse","roof_farm":"module_roof_farm",
    "drone":"module_drone","workshop":"module_workshop",
}
PROP_ALIASES={name:"prop_"+name for name in
              ("tree_broadleaf","tree_conifer","shrub","rocks","bench","lamp","fence",
               "stall","crates","materials","furrows","flowers")}
ENVIRONMENT_ALIASES={"garden_courtyard":"prop_garden_courtyard",
                     "hill_0":"prop_hill_0","hill_1":"prop_hill_1","hill_2":"prop_hill_2"}
BONUS_MODULE_ALIASES={"module_recycling":"module_recycle","module_public_courtyard":"module_open_court",
                      "module_heritage":"module_reuse","module_urban_farm":"module_roof_farm"}


def build_catalog(entries):
    assets={}
    for asset_id,item in entries.items():
        path=item["path"]
        image=bpy.data.images.load(str(ROOT/path),check_existing=False)
        w,h=map(int,image.size)
        bpy.data.images.remove(image)
        bounds=item.get("bounds") or [0,0,w,h]
        cam_units=ORTHO_WORLD_UNITS
        asset={"id":asset_id,"path":path,"size":[w,h],
               "ground_anchor":item["ground_anchor"],"bounds":list(bounds),
               "world_bounds":item["world_bounds"],"ortho_world_units":item.get("ortho_world_units",cam_units),
               "pixels_per_world_unit":w/float(item.get("ortho_world_units",cam_units)),
               "tier":item.get("tier"),"kind":item["kind"],
               "camera":"orthographic; direction vector (9,-12,13.2); fixed orientation, asset anchor sets framing",
               "source_script":"tools/make_art040.py"}
        for field in ("footprint_world_bounds", "roof_world_bounds", "connector_anchors"):
            if item.get(field) is not None:
                asset[field]=item[field]
        if item.get("module_anchors"):
            asset["module_anchors"]=item["module_anchors"]
        assets[asset_id]=asset
    bindings={f"building.{key}":f"building_{key}" for key in BUILDINGS}
    # For older callers that key by the bare definition id.
    bindings.update({key:f"building_{key}" for key in BUILDINGS})
    aliases={}
    aliases.update(OLD_BUILDING_ALIASES)
    aliases.update(OLD_MODULE_ALIASES)
    aliases.update(PROP_ALIASES)
    aliases.update(ENVIRONMENT_ALIASES)
    aliases.update(BONUS_MODULE_ALIASES)
    catalog={
        "schema":"met.art.040", "style":"laisheng_near_future_open_colony_miniature_3d",
        "render_pipeline":"Blender 4.5.1 LTS; original parameterized 3D geometry, hand-authored mineral materials and EEVEE area-light shading",
        "world_units":"Blender world units; X and Y are footprint axes, Z is height; the fixed camera faces the negative-Y front",
        "camera":{"direction_vector_to_camera":[9,-12,13.2],"elevation_degrees":41.35,"azimuth_degrees":-53.1301,
                  "aim_point_offset_screen_up_world_units":(0.5-ANCHOR_NDC_Y)*ORTHO_WORLD_UNITS,
                  "orthographic_scale_world_units":ORTHO_WORLD_UNITS,
                  "prop_scales_world_units":{"common_props":PROP_VIEW_UNITS,"modules":MODULE_VIEW_UNITS,
                                             "terrain_hills":TERRAIN_VIEW_UNITS,"courtyard":COURTYARD_VIEW_UNITS},
                  "ground_projection_compression":0.661,"height_projection_compression":0.751,
                  "ground_anchor_normalized":[0.5,1.0-ANCHOR_NDC_Y]},
        "assets":assets,
        "aliases":aliases,
        "bindings":bindings,
        "legacy_aliases":{"buildings":OLD_BUILDING_ALIASES,"modules":OLD_MODULE_ALIASES,
                          "props":PROP_ALIASES,
                          "environment":{"rocks":"prop_rocks",**ENVIRONMENT_ALIASES}},
        "art_direction":{"body_shadow":"soft form shadow from left-above; no ground-plane/contact shadow baked into PNG",
                          "ground_shadow":"draw one shared soft game-side ground shadow beneath each footprint",
                          "light":"neutral-warm upper-left daylight softened by a large area key and broad frontal fill",
                          "palette":["warm mineral light walls","blue-gray roof and service metal","restrained clay and ochre accents","muted fern and lichen greens"]}
    }
    CATALOG_PATH.parent.mkdir(parents=True,exist_ok=True)
    CATALOG_PATH.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    return catalog


def pixel_anchor(size):
    return [size*.5, size*(1.0-ANCHOR_NDC_Y)]


def render_assets(keys, previews=False):
    entries={}
    for key in keys:
        asset_id="building_"+key
        path=RUNTIME/"buildings"/(key+".png")
        _,world_bounds,footprint_bounds,roof_bounds=render_one(key,"building",path,BUILDING_SIZE)
        bbox=png_alpha_bounds(path)
        xmin,ymin,xmax,ymax=footprint_bounds
        cx,cy=(xmin+xmax)*.5,(ymin+ymax)*.5
        entries[asset_id]={"path":path.relative_to(ROOT).as_posix(),"ground_anchor":pixel_anchor(BUILDING_SIZE),
                           "bounds":bbox,"world_bounds":world_bounds,"ortho_world_units":ORTHO_WORLD_UNITS,
                           "footprint_world_bounds":footprint_bounds,"roof_world_bounds":roof_bounds,
                           "connector_anchors":{"front":[cx,ymin],"right":[xmax,cy],"rear":[cx,ymax],"left":[xmin,cy]},
                           "tier":TIERS[key],"kind":"building"}
        print("RENDERED",asset_id,"size",BUILDING_SIZE,"bbox",bbox,flush=True)
    return entries


def render_props(keys):
    entries={}
    for key in keys:
        output_id=MODULE_ASSET_IDS.get(key,"prop_"+key)
        folder="modules" if key in MODULE_ASSET_IDS else ("terrain" if key.startswith("hill_") else "props")
        path=RUNTIME/folder/(key+".png")
        _,world_bounds,footprint_bounds,roof_bounds=render_one(key,"prop",path,PROP_SIZE)
        bbox=png_alpha_bounds(path)
        entries[output_id]={"path":path.relative_to(ROOT).as_posix(),"ground_anchor":pixel_anchor(PROP_SIZE),
                            "bounds":bbox,"world_bounds":world_bounds,"ortho_world_units":camera_scale("prop",key),
                            "footprint_world_bounds":footprint_bounds,"roof_world_bounds":roof_bounds,
                            "kind":"module" if key in MODULE_ASSET_IDS else "prop"}
        print("RENDERED",output_id,"size",PROP_SIZE,flush=True)
    return entries


def add_contact_sheet():
    from PIL import Image, ImageDraw, ImageFont
    catalog=json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    items=list(catalog["assets"].items())
    cell_w,cell_h=222,224
    cols=6; rows=math.ceil(len(items)/cols)
    sheet=Image.new("RGB",(cols*cell_w,rows*cell_h),(215,222,221))
    draw=ImageDraw.Draw(sheet)
    try: font=ImageFont.truetype("/System/Library/Fonts/PingFang.ttc",15)
    except Exception: font=ImageFont.load_default()
    for i,(aid,item) in enumerate(items):
        x=(i%cols)*cell_w;y=(i//cols)*cell_h
        draw.rounded_rectangle((x+4,y+4,x+cell_w-4,y+cell_h-4),radius=10,
                               fill=(235,236,229),outline=(133,151,157),width=2)
        im=Image.open(ROOT/item["path"]).convert("RGBA")
        im.thumbnail((cell_w-24,cell_h-48),Image.Resampling.LANCZOS)
        sheet.paste(im,(x+(cell_w-im.width)//2,y+3),im)
        label=aid.replace("building_","b:").replace("module_","m:").replace("prop_","p:")
        draw.text((x+9,y+cell_h-25),label,fill=(44,62,70),font=font)
    out=SOURCE/"visual040_contact_sheet.png"
    out.parent.mkdir(parents=True,exist_ok=True)
    sheet.save(out)
    print("CONTACT_SHEET",out,flush=True)


def render_tier_preview():
    """Render one source-only representative from each populated tier."""
    global bpy
    import bpy
    TIER_PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    for key in ("homestead", "residence", "advanced_residence"):
        out = TIER_PREVIEW_DIR / (key + ".png")
        render_one(key, "building", out, BUILDING_SIZE)
        print("TIER_PREVIEW", key, out, flush=True)


def add_tier_preview_contact():
    """Make an anchor-aligned T1/T2/T3 contact sheet outside runtime assets."""
    from PIL import Image, ImageDraw, ImageFont
    items = (("T1 · open settlement", "homestead"),
             ("T2 · permanent town", "residence"),
             ("T3 · midrise city block", "advanced_residence"))
    cell_w, cell_h = 440, 500
    sheet = Image.new("RGB", (cell_w * len(items), cell_h), (216, 223, 222))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/PingFang.ttc", 22)
    except Exception:
        font = ImageFont.load_default()
    scale = .49
    anchor_y = 367
    for i, (label, key) in enumerate(items):
        x = i * cell_w
        draw.rectangle((x + 8, 8, x + cell_w - 8, cell_h - 8), fill=(228, 233, 231), outline=(157, 170, 172), width=2)
        source = Image.open(TIER_PREVIEW_DIR / (key + ".png")).convert("RGBA")
        w = int(source.width * scale)
        h = int(source.height * scale)
        sprite = source.resize((w, h), Image.Resampling.LANCZOS)
        anchor_px = BUILDING_SIZE * (1.0 - ANCHOR_NDC_Y)
        sheet.paste(sprite, (x + (cell_w - w) // 2, anchor_y - int(anchor_px * scale)), sprite)
        draw.line((x + 18, anchor_y, x + cell_w - 18, anchor_y), fill=(119, 134, 136), width=1)
        draw.text((x + 18, 24), label, fill=(39, 55, 61), font=font)
        draw.text((x + 18, 472), key.replace("_", " "), fill=(60, 74, 78), font=font)
    out = SOURCE / "laisheng_tier_contact_preview.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print("TIER_CONTACT", out, flush=True)


def parse_args():
    argv=sys.argv
    if "--" in argv: argv=argv[argv.index("--")+1:]
    else: argv=[]
    p=argparse.ArgumentParser()
    p.add_argument("--preview",action="store_true")
    p.add_argument("--tier-preview",action="store_true",
                   help="render source-only T1, T2 and T3 representatives")
    p.add_argument("--tier-contact",action="store_true",
                   help="assemble the anchor-aligned representative contact sheet")
    p.add_argument("--all",action="store_true")
    p.add_argument("--props",action="store_true")
    p.add_argument("--props-only",nargs="+",choices=PROPS,
                   help="render selected shared decoration assets")
    p.add_argument("--contact",action="store_true")
    p.add_argument("--source-scene",choices=BUILDINGS,
                   help="save an editable Blender source scene for one building")
    p.add_argument("--only",nargs="+")
    return p.parse_args(argv)


def main():
    args=parse_args()
    if args.contact:
        add_contact_sheet()
        return
    if args.tier_contact:
        add_tier_preview_contact()
        return
    global bpy
    import bpy
    if args.source_scene:
        save_source_scene(args.source_scene)
        return
    if args.tier_preview:
        render_tier_preview()
        return
    if args.props_only:
        entries=render_props(args.props_only)
        existing=json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["assets"]
        existing.update(entries)
        build_catalog(existing)
        print("CATALOG",CATALOG_PATH,"assets",len(existing),flush=True)
        return
    if args.preview:
        selected=["homestead","neighborhood","workshop","advanced_residence"]
        entries=render_assets(selected,True)
        build_catalog(entries)
        print("PREVIEW_CATALOG",CATALOG_PATH,flush=True)
        return
    if args.props:
        entries=render_props(PROPS)
        existing={}
        if CATALOG_PATH.exists():
            try: existing=json.loads(CATALOG_PATH.read_text(encoding="utf-8")).get("assets",{})
            except Exception: existing={}
        existing.update(entries)
        build_catalog(existing)
        print("CATALOG",CATALOG_PATH,"assets",len(existing),flush=True)
        return
    selected=args.only or BUILDINGS
    entries=render_assets(selected)
    if args.all or not args.only:
        entries.update(render_props(PROPS))
    # If the render is split across batches, merge the exact metadata already
    # on disk so the final catalog still names every shipped asset.
    existing={}
    if CATALOG_PATH.exists():
        try: existing=json.loads(CATALOG_PATH.read_text(encoding="utf-8")).get("assets",{})
        except Exception: existing={}
    existing.update(entries)
    build_catalog(existing)
    print("CATALOG",CATALOG_PATH,"assets",len(existing),flush=True)


if __name__=="__main__":
    main()
