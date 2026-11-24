import bpy, math, mathutils
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
bpy.ops.import_scene.gltf(filepath=r"C:\Users\harro\DeVinci Kart\Backup_Modeles\Lakitu_feu_source.glb")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
arm.show_in_front=True; arm.data.show_names=True; arm.data.display_type='STICK'
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.color_type='TEXTURE'; sc.display.shading.light='STUDIO'
sc.render.resolution_x=sc.render.resolution_y=600
cd=bpy.data.cameras.new("c"); cam=bpy.data.objects.new("c",cd); sc.collection.objects.link(cam); sc.camera=cam
import bmesh
dg=bpy.context.evaluated_depsgraph_get()
bb=[mesh.matrix_world@mathutils.Vector(c) for c in mesh.bound_box]
ctr=sum(bb,mathutils.Vector())/8; R=max((b-ctr).length for b in bb)
cd.type='ORTHO'; cd.ortho_scale=R*2.2
groups=[("bras_A",["bone_3","bone_4","bone_5","bone_6","bone_7","bone_8","bone_9","bone_10","bone_11"]),
        ("chaine_B",["bone_16","bone_17","bone_18","bone_19","bone_20","bone_21","bone_22","bone_23"]),
        ("tete",["tripo::Head_1","tripo::Head_2","tripo::Head_3","tripo::Spine_0","bone_15"]),
        ("chaine_C",["bone_27","bone_28","bone_29","bone_30","bone_31"]),
        ("divers",["bone_24","bone_25","bone_26"])]
views={"face":(0,-1,0),"cote":(1,0,0),"dessus":(0,0,1)}
def shot(name):
    for vn,d in views.items():
        d=mathutils.Vector(d)
        cam.location=ctr+d*R*3; cam.rotation_euler=(-d).to_track_quat('-Z','Y' if vn!="dessus" else 'Y').to_euler()
        sc.render.filepath=rf"C:\Users\harro\DeVinci Kart\Backup_Modeles\os\{name}_{vn}.png"
        bpy.ops.render.render(write_still=True)
shot("repos")
pb=arm.pose.bones
print("BONES",[b.name for b in pb])
for g,names in groups:
    for b in pb: b.rotation_mode='XYZ'; b.rotation_euler=(0,0,0); b.rotation_quaternion=(1,0,0,0)
    for n in names:
        if n in pb: pb[n].rotation_mode='XYZ'; pb[n].rotation_euler=(math.radians(25),0,math.radians(15))
    bpy.context.view_layer.update()
    shot(g)
for b in pb: b.rotation_mode='QUATERNION'; b.rotation_quaternion=(1,0,0,0)
print("OS_OK", ctr, R)
