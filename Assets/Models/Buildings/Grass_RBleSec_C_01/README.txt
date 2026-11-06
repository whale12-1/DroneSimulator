3D Texel - Grass_RBleSec_C_01
Universal vegetation asset (variant C, sparse): Unreal Engine, Unity, Blender, Godot. 3D Texel License (LICENSE_3DTexel.txt).
One static mesh, ready to instance (foliage painter, scatter tools, HISM): pivot on the ground at the centre of the clump.

FILES
  SM_Grass_RBleSec_C_01.fbx    Main file. FBX 7.4 binary, centimetres, +Y up. ONE LOD group:
              SM_Grass_RBleSec_C_01_LOD0, _LOD1, _LOD2 (cards) and _LOD3 (impostor: clusters of planes).
              Textures are NOT embedded: the FBX points to Textures/.
  SM_Grass_RBleSec_C_01.gltf   Same 4 LODs for Blender / Godot / web, metres, materials ready (alpha clip, two-sided cards).
  SM_Grass_RBleSec_C_01.bin    Geometry of the .gltf (keep it next to the .gltf).
  Textures/   T_..._D.png    base colour (sRGB) + ALPHA = opacity (clip 0.5)
              T_..._N.png    normal map, OpenGL convention (+Y green)
              T_..._ORM.png  linear: R = ambient occlusion, G = roughness, B = metallic (0)
              T_..._Impostor_D/_N/_ORM.png   same roles for the impostor (last LOD)
  txveg.json  Manifest (LOD distances, materials, textures, wind, Foliage Type values). Read by the 3D Texel Portal plugin.

UNREAL ENGINE (5.3+)
  Easiest: 3D Texel Portal plugin -> drag the .txveg into the Content Browser (mesh, LODs, wind material, Foliage Type).
  Manual (drag the .fbx, Interchange - default in UE 5.5+): you get ONE static mesh with 4 LODs. In the import dialog
  UNTICK "Build Nanite" (with Nanite the LODs are ignored), then in the Static Mesh editor untick "Auto Compute LOD
  Screen Size" and type the values of txveg.json (lods[].screen_size: 1 / 0.5 / 0.25 / 0.1).
  Legacy FBX importer: tick "Import Mesh LODs" (otherwise only LOD0 comes in), Vertex Color Import Option = Replace.
  Do not import the .gltf in Unreal (it has no LOD group: separate meshes).
  Textures: _N -> tick "Flip Green Channel" (Unreal expects DirectX normals); _ORM -> Compression "Masks", sRGB off.
  Material: Masked (clip 0.5), Two Sided (cards) / One Sided (impostor), Opacity Mask = alpha of _D.

UNITY (2021+, Built-in / URP / HDRP)
  Drop the whole folder into Assets/. The _LOD0.._LOD3 suffixes create a LOD Group automatically.
  Material: Alpha Clipping 0.5, Render Face = Both for the cards (Front for the impostor). Normal map is already OpenGL.
  Optional: set the LOD transitions from txveg.json (lods[].screen_size).

BLENDER (3.6+)
  File > Import > glTF 2.0 (.gltf): materials are ready. Hide LOD1-3 to work with LOD0 only.
  The FBX also works (File > Import > FBX): objects come in at scale 0.01 (centimetres), apply the scale.

GODOT (4.3+)
  Drop the folder into the project and use the .gltf (materials ready: alpha scissor, two-sided cards).
  Set "Visibility Range" on each LOD node with the distances you want (or keep LOD0 only).
  In shaders: CUSTOM0 = pivots (UV2, UV3), CUSTOM1 = wind masks RGBA, COLOR = brightness.
  If you use the .fbx instead, untick "Vertex Color > Use As Albedo" in its materials (the FBX colours are wind masks).

WIND MASKS (all LODs)
  FBX vertex colour / glTF attribute _WIND:
    R = plant sway (0 at the ground -> 1 at the top)   G = blade bend (0 card root -> 1 tip)
    B = tip tremble                                    A = random phase per tuft (0..1)
  UV1 "Look" (dry tint, brightness) - UV2 "Pivot" = card root (x, z) - UV3 "PivotHeight" = (root height, card length),
  in file units (FBX cm, glTF m). Simple wind in any engine:
    offset = wind_dir * strength * (R*0.6 + G*0.4) * sin(time*speed + A*6.283 + dot(world_pos.xz, wind_dir)*0.15)

NAMES  SM_ mesh - _LODn level of detail - MI_ material instance (= FBX material slot) - T_ texture -
       _D / _N / _ORM texture role - FT_ Foliage Type (Unreal, created by the plugin)
