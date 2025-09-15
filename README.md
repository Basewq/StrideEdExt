# Stride Editor Extension for Terrain System

This repository is a proof of concept for extending the Stride Game Engine for a terrain system.

Due to time constraints, this repository does not contain much documentation.

This project has a similar structure to the [Level Editor Extension Example proof of concept project](https://github.com/Basewq/XenkoProofOfConcepts/tree/master/LevelEditorExtensionExample) so many of the quirks/limitations from that project applies here.

---

### Functionalities

- Heightmap editing:
	- Model Heightmap: create a top-down projection of the model and use the model's height as height displacement.
	- Texture Heightmap: Height displacement based on a greyscale texture.
	- Painter Heightmap: Paint with a circular brush or a texture brush.

	
- Material (texture) editing:
	- Texture Weight Map: Weight value based on a greyscale texture.
	- Painter Weight Map: Paint with a circular brush or a texture brush.

Material system is based on 'One Material Per Vertex' technique from the 'Call of Duty' engine (see link down below).
Each terrain vertex can only be exactly one material so weight maps are based on highest value wins rather than trying to blend over an arbitrary number of weight values.


#### Showcase

Heightmaps:

<video controls>
  <source src="./images/model_heightmap_layer_edit.mp4" type="video/mp4">
</video controls>

<video controls>
  <source src="./images/painter_heightmap_layer_edit.mp4" type="video/mp4">
</video controls>

<video controls>
  <source src="./images/texture_heightmap_layer_edit.mp4" type="video/mp4">
</video controls>

Material maps:

<video controls>
  <source src="./images/painter_material_layer_edit.mp4" type="video/mp4">
</video controls>

<video controls>
  <source src="./images/texture_material_layer_edit.mp4" type="video/mp4">
</video controls>

---

#### Todo features

- Rework foliage system to work with terrain system.

### Known issues

- For `Texture * Layer Component`, you must disable 'Generate mipmaps', 'Compress', and 'Stream' on the texture to ensure data can be read properly.
- The in-progress painting may not always correctly preview the final modification. This is more apparent when painting across multiple terrain chunks due to the each terrain chunks having their own paint stroke maps.
- Terrain flickers to black for a single frame after a modification - this is probably due to the material referencing some disposed texture which needs to be fixed (but doesn't appear to be catastrophic, for the time being).
- Editor uses (legacy) Bullet physics to generate static mesh. Bepu has not been implemented.
- Random editor crashes if modifying data too quickly.

---

### Terrain Example

The `TerrainScene` asset contains a working example of the terrain editor system.

There are two asset types for the terrain system:
- Terrain Map
- Terrain Material

**Terrain Map** defines the size & height range of the map and the size of the meshes/terrain chunks.
> The terrain system implemented here does not support dynamic map chunk streaming or infinite world.

For the terrain to actually be used in the game, you will need to add a `Terrain Component` to an entity, which is where you assign the `Terrain Map` and the camera.
The terrain meshes are created and destroyed based on camera visibility and the chunk settings on the `Terrain Map` asset.

Be aware the physics collider is generated for the entire terrain map and its entirety is placed in the world (ie. it is **not** dynamically generated based on the camera view).


**Terrain Material** defines 'layers' which are the textures that a terrain can use when assigned to this specific `Terrain Material` asset.
The textures are grouped by diffuse, normal map, and height blend map and packed in a texture array.


The editing is done on a sub-scene (via an entity with a `Terrain Map Editor Component` assigned with the `Terrain Map` asset), which is actually exploiting some quirk in the Stride Editor.
This allows us to add 'Layer Component's to modify the terrain, but at run-time this sub-scene is not loaded and only the compiled `Terrain Map` data is used.

The `* Heightmap Layer Component`s and `* Material Weight Map Layer Component`s should be place in entities as direct children of the entity with the `Terrain Map Editor Component` (grouping with folders is allowed).

> Layers are prioritized like general paint software - higher level layers take priority over lower levels.

For `Texture * Layer Component`, you must disable 'Generate mipmaps', 'Compress', and 'Stream' on the texture to ensure data can be read properly.
These should probably also be disabled for painter texture brushes, to avoid issues any potential issues with the stroke map render output.


---

This project takes some inspiration from the following:


Terrain material blending: [Advanced Graphics Summit: Boots on the Ground: The Terrain of 'Call of Duty'](https://www.gdcvault.com/play/1027463/Advanced-Graphics-Summit-Boots-on)
- [Power point slides](https://research.activision.com/publications/2021/09/boots-on-the-ground--the-terrain-of-call-of-duty) for the COD talk.


Generic painting system: [RE:2023 Creating a Real-Time, In-Engine Painting Tool](https://www.youtube.com/watch?v=xGmVGI-_-Zg)

Asset undo/redo transaction & editor/run-time message passing: [Creating a Tools Pipeline for Horizon: Zero Dawn](https://www.youtube.com/watch?v=KRJkBxKv1VM)


---

Terrain texture assets in [./StrideEdExt.Game/Resources/Terrain/textures](./StrideEdExt.Game/Resources/Terrain/textures) folder come from https://polyhaven.com
> The license for these textures are [CC0](https://polyhaven.com/license)
