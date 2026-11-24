# DeVinci Kart - Lakitu : ajoute 3 vraies lampes sur le feu (peint dans la texture) pour le départ façon Mario Kart.
# Travaille dans une NOUVELLE scène (la scène ouverte n'est pas touchée), puis exporte en .glb dans le projet Unity.
import bpy, math
from mathutils import Vector

SRC = r"C:\Users\harro\DeVinci Kart\Backup_Modeles\Lakitu_feu_source.glb"
DST = r"C:\Users\harro\DeVinci Kart\Assets\3DModels\Lakitu.glb"
# centres des 3 lampes peintes dans la texture (u, v de Blender = 1 - v du glTF), rayon en UV
LAMPS_UV = [(0.567, 1 - 0.722), (0.567, 1 - 0.827), (0.567, 1 - 0.934)]
R_UV = 0.0306

scene = bpy.data.scenes.new("Lakitu_Feu")
bpy.context.window.scene = scene
bpy.ops.import_scene.gltf(filepath=SRC)

mesh_obj = max((o for o in scene.objects if o.type == 'MESH'), key=lambda o: len(o.data.polygons))
me = mesh_obj.data
uvl = me.uv_layers.active.data
mw = mesh_obj.matrix_world
me.calc_loop_triangles()

def locate(u, v):
    """Point 3D (monde), normale et échelle UV->mètres du point (u, v) de la texture."""
    for tri in me.loop_triangles:
        l0, l1, l2 = tri.loops
        A, B, C = (Vector(uvl[l].uv) for l in (l0, l1, l2))
        v0, v1, v2 = C - A, B - A, Vector((u, v)) - A
        d00, d01, d11 = v0.dot(v0), v0.dot(v1), v1.dot(v1)
        d20, d21 = v2.dot(v0), v2.dot(v1)
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14:
            continue
        a = (d11 * d20 - d01 * d21) / den
        b = (d00 * d21 - d01 * d20) / den
        if a < -1e-6 or b < -1e-6 or a + b > 1 + 1e-6:
            continue
        PA, PB, PC = (mw @ me.vertices[me.loops[l].vertex_index].co for l in (l0, l1, l2))
        p = PA + a * (PC - PA) + b * (PB - PA)
        n = (PB - PA).cross(PC - PA).normalized()
        # échelle : longueur 3D d'un déplacement UV
        s = ((PC - PA).length / max((C - A).length, 1e-9) + (PB - PA).length / max((B - A).length, 1e-9)) * 0.5
        return p, n, s
    return None

# la face du feu est tournée vers l'extérieur : on oriente la normale à l'opposé du centre du modèle
center = sum((mw @ v.co for v in me.vertices), Vector()) / len(me.vertices)
made = []
for k, (u, v) in enumerate(LAMPS_UV, start=1):
    hit = locate(u, v)
    if hit is None:
        print("Lampe", k, "introuvable")
        continue
    p, n, s = hit
    if n.dot(p - center) < 0:
        n = -n
    r = R_UV * s * 0.92
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=r, depth=r * 0.25, location=p + n * (r * 0.14))
    lamp = bpy.context.active_object
    lamp.name = f"Lampe_{k}"
    lamp.data.name = f"Lampe_{k}"
    lamp.rotation_euler = n.to_track_quat('Z', 'Y').to_euler()
    mat = bpy.data.materials.new(f"Lampe_{k}_Mat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.05, 0.05, 0.06, 1)
    bsdf.inputs["Roughness"].default_value = 0.25
    lamp.data.materials.append(mat)
    # suit le feu : enfant de l'objet du modèle (même repère que Lakitu dans Unity)
    lamp.parent = mesh_obj
    lamp.matrix_parent_inverse = mesh_obj.matrix_world.inverted()
    made.append(lamp.name)

bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', use_active_scene=True)
print("LAKITU_FEU_OK", made, "->", DST)
