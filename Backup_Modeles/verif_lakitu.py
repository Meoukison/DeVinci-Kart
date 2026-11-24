import bpy, mathutils
if not any(o.name.startswith("Lampe_") for o in bpy.data.objects): bpy.ops.import_scene.gltf(filepath=r"C:\Users\harro\DeVinci Kart\Assets\3DModels\Lakitu.glb")
L=[o for o in bpy.data.objects if o.name.startswith("Lampe_")]
L.sort(key=lambda o:o.name)
print("LAMPES",[o.name for o in L])
cols=[(1,0.05,0.03,1),(1,0.05,0.03,1),(0.1,1,0.2,1)]
for o,c in zip(L,cols):
    for s in o.material_slots: s.material.diffuse_color=c
mid=L[1].matrix_world.translation.copy(); n=(L[1].matrix_world.to_3x3()@mathutils.Vector((0,0,1))).normalized()
sc=bpy.context.scene
camd=bpy.data.cameras.new("c"); cam=bpy.data.objects.new("c",camd); sc.collection.objects.link(cam); sc.camera=cam
def shot(pos,path):
    cam.location=pos; cam.rotation_euler=(mid-pos).to_track_quat('-Z','Y').to_euler()
    sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.color_type='TEXTURE'; sc.display.shading.light='FLAT'
    sc.render.resolution_x=sc.render.resolution_y=700; sc.render.filepath=path
    bpy.ops.render.render(write_still=True)
d=0
for o in bpy.data.objects:
    if o.type=='MESH': d=max(d,max(o.dimensions))
shot(mid+n*0.35*d+mathutils.Vector((0,0,0.0)), r"C:\Users\harro\DeVinci Kart\Backup_Modeles\verif_proche.png")
shot(mid+n*1.6*d+mathutils.Vector((0,0,0.3*d)), r"C:\Users\harro\DeVinci Kart\Backup_Modeles\verif_loin.png")
print("VERIF_OK",[o.name for o in L], n, d)
