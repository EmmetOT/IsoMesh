# IsoMesh
IsoMesh is a group of related tools for Unity for converting meshes into signed distance field data, raymarching signed distance fields, and extracting signed distance field data back to meshes via surface nets or dual contouring. All the work is parallelized on the GPU using compute shaders.

这个项目是一个用于Unity的相关工具集，可将网格转换为有符号距离场数据（Signed Distance Field，SDF），通过光线行进SDF以及通过表面网格或双重等值面提取SDF数据并还原为网格。所有这些工作都是在GPU上使用计算着色器并行处理的。

My motivation for making this was simple: I want to make a game in which I morph and manipulate meshes, and this seemed like the right technique for the job. Isosurface extraction is most often used for stuff like terrain manipulation. Check out No Man's Sky, for example.

制作这个项目的动机很简单：我想制作一个游戏，在游戏中我可以改变和操作网格，而这似乎是合适的技术。等值面提取通常用于类似地形操作的任务。例如，可以查看《无人之境》（No Man's Sky）。

I decided to share my code here because it represents a lot of trial and error and research on my part. And frankly, I just think it's cool.

我决定在这里分享我的代码，因为它代表了我付出了很多试验、错误和研究的努力。而且，说实话，我觉得这很酷。

![isomesh10](https://user-images.githubusercontent.com/18707147/121936495-99b0c800-cd41-11eb-910b-83113b790c86.gif)

The project is currently being developed and tested on Unity 2021.2.0f1.

## Signed Distance Fields
A signed distance field, or 'SDF', is a function which takes a position in space and returns the distance from that point to the surface of an object. The distance is negative if the point is inside the object. These functions can be used to represent all sorts of groovy shapes, and are in some sense 'volumetric', as opposed to the more traditional polygon-based way of representing geometry.

If you're unfamiliar with SDFs, I would be remiss if I didn't point you to the great [Inigo Quilez](https://www.iquilezles.org/). I'm sure his stuff will do a much better job explaining it than I could.

有符号距离场，或称为'SDF'，是一个函数，它接受空间中的一个位置并返回该点到物体表面的距离。如果点在物体内部，距离是负数。这些函数可用于表示各种复杂的形状，从某种程度上说是“体积化”的，与更传统的基于多边形的几何表示方法不同。

如果你对SDF不熟悉，我应该指出伊尼戈·基莱斯（Inigo Quilez）有很多关于它的很棒的内容。我相信他的内容会比我更好地解释它。

### Signed Distance Fields + Meshes
While SDFs are really handy, they're mostly good for representing primitive shapes like spheres, cuboids, cones, etc. You can make some pretty impressive stuff just by combining and applying transformations to those forms, but for this project I wanted to try combine the mushy goodness of SDFs with the versatility of triangle meshes. I do this by sampling points in a bounding box around a mesh, and then interpolating the in-between bits.

虽然SDF非常方便，但主要用于表示诸如球体、立方体、圆锥体等基本形状。通过组合和应用这些形状的变换，你可以创建一些令人印象深刻的内容，但在这个项目中，我想尝试将SDF的柔软性与三角网格的多用途性结合起来。我通过在网格周围的一个边界框内对点进行采样，然后进行插值来实现这一点。
#### Adding Meshes
In order to add a mesh of your own, open Tools > 'Mesh to SDF'. Give it a mesh reference and select a sample size, I suggest 64. Remember this is cubic, so the workload and resulting file size increases very quickly.

为了添加你自己的网格，请打开“工具” > “网格到SDF”。给它一个网格引用，并选择一个采样大小，我建议选择64。请记住，这是一个立方体，因此工作量和结果文件大小会非常迅速增长。

There is also the option to tessellate the mesh before creating the SDF. This will take time and increase the GPU workload, but it will not alter the size of the resulting file. The advantage of the tessellation step is that the resulting polygons will have their positions interpolated according to the normals of the source vertices, turning the 'fake' surfaces of normal interpolation into true geometry. This can produce smoother looking results, but it's usually unnecessary.

还有一个选项，可以在创建SDF之前对网格进行镶嵌。这将花费时间并增加GPU的工作量，但不会改变结果文件的大小。镶嵌步骤的优点是，生成的多边形将根据源顶点的法线进行位置插值，将法线插值的“虚拟”表面转换为真实几何形状。这可以产生更平滑的结果，但通常是不必要的。

If your mesh has UVs you can sample those too. This is currently just experimental: naively sampling UVs has a big assumption built in - namely that your UVs are continuous across ths surface of your mesh. As soon you hit seams you'll see pretty bad artefacts as the UVs rapidly interpolate from one part of your texture to the other.

如果你的网格具有UV，你也可以对其进行采样。目前这只是实验性的：天真地对UV进行采样有一个很大的假设，即你的UV在整个网格表面上是连续的。一旦你遇到接缝，你会看到相当严重的伪像，因为UV将从一个部分迅速插值到另一个部分。

![isomesh1](https://user-images.githubusercontent.com/18707147/115974173-d686ec80-a552-11eb-8308-87ddec99cd16.png)

## Project Structure
In this project, you'll find some sample scenes, including one demonstrating mesh generation and one demonstrating raymarching. Both have very similar structures and are just meant to show how to use the tools.

在这个项目中，你会找到一些示例场景，包括一个演示网格生成和一个演示光线行进的场景。它们的结构非常相似，只是展示了如何使用这些工具。

SDF objects are divided into three different components. These objects can be set to either 'min' or 'subtract' - min (minimum) objects will combine with others, subtract objects will 'cut holes' in all the objects above them in the hierarchy. These objects can be added to the scene by right-clicking in the hierarchy.

SDF对象分为三个不同的组件。这些对象可以设置为“min”或“subtract” - “min”（最小）对象将与其他对象组合，而“subtract”对象将在其上面的所有对象中“挖洞”。你可以通过在层次视图中右键单击来将这些对象添加到场景中。

![isomesh8](https://user-images.githubusercontent.com/18707147/118728208-b2ee5380-b82b-11eb-8467-1c533c6d352b.png)

### SDFPrimitives
The SDFPrimitive component is standalone and can only currently represent four objects: a sphere, a (rounded) cuboid, a torus, and a box frame. Each of them has a few unique parameters, and they have nice reliable UVs. For now these are the only SDF primitives and operations I've added, [but there are many more.](https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm)

SDFPrimitive组件是独立的，目前只能表示四种对象：球体、（圆角）长方体、环体和框架。每种对象都有一些独特的参数，它们具有可靠的UV。目前这些是唯一的SDF原语和操作，[但还有更多](https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm)。
### SDFMeshes
SDFMeshes provide a reference to an SDFMeshAsset file generated by the Mesh to SDF tool. These objects behave much as you'd expect from a Unity GameObject: you can move them around, rotate them, etc. 

SDFMeshes提供了对由网格到SDF工具生成的SDFMeshAsset文件的引用。这些对象的行为与你从Unity的GameObject中期望的行为非常相似：你可以移动它们、旋转它们等。
### SDFOperations
Currently there is only one SDFOperation supported - elongation. These works a little differently to primitives and meshes. An operation deforms the space of everything *below* it in the hierarchy. The elongation operation allows you to stretch space, which also works on the UVs!

目前只支持一种SDFOperation - 延展（elongation）。这与原语和网格的行为有些不同。操作会改变位于其层次结构下面的所有东西的空间。延展操作允许你拉伸空间，也适用于UV！
![blobbyBricks6](https://user-images.githubusercontent.com/18707147/118727975-90f4d100-b82b-11eb-871f-6aa2e96e8cc6.gif)

You'll always have an 'SDFGroup' as the parent of everything within your system of SDFMeshes and SDFPrimitives. Objects within this system are expected to interact with one another and share the same visual properties such as colour and texture.

你的系统中的所有东西都应该有一个'SDFGroup'作为父对象，包括SDFMeshes和SDFPrimitives。这些对象预计会相互作用，并共享相同的视觉属性，如颜色和纹理。

The final essential element is an 'SDFGroupRaymarcher' or 'SDFGroupMeshGenerator'. You can have as many of these as you want under one group. SDFGroupMeshGenerators can even represent chunks - though they need to overlap by the size of one cell on all sides, and should have all the same settings.

最后一个关键元素是'SDFGroupRaymarcher'或'SDFGroupMeshGenerator'。你可以在一个组下拥有尽可能多的这些。SDFGroupMeshGenerators甚至可以表示块 - 尽管它们需要在所有边都有一个单元格的大小重叠，并且应具有相同的设置。
## Isosurface Extraction 
等值面提取

Given a scalar field function, which returns a single value for any point, an isosurface is everywhere the function has the same value. In SDFs, the isosurface is simply the points where the distance to the surface is zero.

对于一个标量场函数，它为任何点返回一个值，等值面是函数在该点具有相同值的地方。在SDF中，等值面就是距离表面为零的点。

Isosurface extraction here refers to converting an isosurface back into a triangle mesh. There are many different algorithms for isosurface extraction, perhaps the most well known being the 'marching cubes' algorithm. In this project, I implemented two (very similar) isosurface extraction algorithms: surface nets and dual contouring. I won't go into any more detail on these algorithms here, [as others have already explained them very well.](https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/)

这里的等值面提取是指将等值面转换回三角网格。等值面提取有许多不同的算法，其中最知名的可能是“Marching Cubes”算法。在这个项目中，我实现了两种（非常相似的）等值面提取算法：surface nets和dual contouring。我不会在这里详细介绍这些算法，[因为其他人已经很好地解释过了](https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/)。

As I say above, in order to use the isosurface extraction just add an SDFGroupMeshGenerator under an SDFGroup. The number of options on this component is almost excessive, but don't let that get you down, they all have tooltips which do some explaining, and if you've done your homework they should feel fairly familiar:

正如我在上面提到的，要使用等值面提取，只需在SDFGroup下添加一个SDFGroupMeshGenerator。这个组件的选项数量几乎有点多，但不要灰心，它们都有工具提示来解释，如果你做了功课，它们应该感觉相当熟悉：


![isomesh10](https://user-images.githubusercontent.com/18707147/120230970-4ec68900-c248-11eb-9669-d531776f0227.png)

Normal settings are handy to control the appearance of the mesh surface. 'Max angle tolerance' will generate new mesh vertices when normals are too distinct from the normal of their triangle. I like to keep this value around 40 degrees, as it retains sharp edges while keeping smooth curves. 'Visual smoothing' changes the distance between samples when generating mesh normals via central differences.

法线设置非常方便，可以控制网格表面的外观。当法线与其三角形的法线明显不同时，“最大角度容差”将生成新的网格顶点。我喜欢将这个值保持在大约40度左右，因为它保留了锐利的边缘，同时保持了平滑的曲线。“视觉平滑”在通过中心差异生成网格法线时改变采样点之间的距离。

![isomesh4](https://user-images.githubusercontent.com/18707147/115974786-21a2fe80-a557-11eb-84a0-62c28b537501.png)

I provide two techniques for finding the exact surface intersection points between SDF samples - interpolation is fast but gives kinda poor results at corners. Binary search provides much more exact results but is an iterative solution.

我提供两种查找SDF样本之间精确表面交点的方法 - 插值是快速的，但在角落处效果不太好。二分查找提供了更精确的结果，但是它是一个迭代的解决方案。

Gradient descent is another iterative improvement which simply moves the vertices back onto the isosurface. Honestly, I see no reason not to always have this on.

梯度下降是另一种迭代的改进方法，它简单地将顶点移回等值面上。老实说，我认为总是应该启用这个选项。

## Raymarching
If you're familiar with SDFs, you're familiar with raymarching. They very often go hand-in-hand. [Raymarching will also be very familiar to you if you ever go on ShaderToy.](https://www.shadertoy.com/results?query=raymarch) Again I recommend checking out Inigo Quilez for an in-depth explanation, but raymarching is basically an iterative sort of 'pseudo-raytracing' algorithm for rendering complex surfaces like SDFs.

如果你熟悉SDF，你应该也熟悉光线行进。它们经常一起使用。[如果你经常去ShaderToy，你应该对光线行进非常熟悉。](https://www.shadertoy.com/results?query=raymarch)同样，我建议查看伊尼戈·基莱斯的内容，他详细解释了光线行进，但光线行进基本上是一种迭代的“伪射线追踪”算法，用于渲染复杂的表面，如SDF。

In this project you can use an SDFGroupRaymarcher to visualize your SDFGroup. This component basically just creates a cube mesh and assigns it a special raymarching material. The resulting visual is much more accurate than the isosurface extraction, but it's expensive just to look at: unlike isosurface extraction which is just doing nothing while you're not manipulating the SDFs, raymarching is an active process on your GPU.

在这个项目中，你可以使用SDFGroupRaymarcher来可视化你的SDFGroup。这个组件基本上只是创建一个立方体网格，并为其分配一个特殊的光线行进材质。结果的视觉效果要比等值面提取更精确，但仅仅是查看时非常昂贵：与等值面提取不同，光线行进在你不操纵SDF时也在主动处理。

The raymarching material is set up to be easy to modify - it's built around this subgraph:

光线行进材质设置得非常容易修改 - 它建立在这个子图之上：

![isomesh5](https://user-images.githubusercontent.com/18707147/115975216-c5da7480-a55a-11eb-9452-18740e81286b.png)

'Hit' is simply a 0/1 int which tells you whether a surface was hit, 'Normal' is that point's surface normal, and that should be enough to set up your shader. I also provide a 'thickness' value to start you on your way to subsurface scattering. Neat! 

'Hit'只是一个0/1整数，告诉你是否击中了表面，'Normal'是该点的表面法线，这应该足以设置你的着色器。我还提供了一个“厚度”值，让你更容易实现次表面散射。很酷！

It also outputs a UV. UVs are generated procedurally from primitives and for meshes they're sampled. The final UV of each point is a weighted average of the UV of each SDF in the group. Texturing combined shapes can look really funky:

它还输出UV。UV是从原语中生成的，对于网格，它们是采样的。每个点的最终UV是每个SDF在组中的UV的加权平均值。对组合形状进行纹理处理可能会看起来很有趣：

![isomesh6](https://user-images.githubusercontent.com/18707147/115975359-fc64bf00-a55b-11eb-8aa8-b3895448221e.png)

You can also directly visualize the UVs and iteration count.

你还可以直接可视化UV和迭代计数。

![isomesh7](https://user-images.githubusercontent.com/18707147/115975420-8745b980-a55c-11eb-9a6f-416848f5cc9e.png)

## Physics

I also include a very fun sample scene showing how you might add physical interaction. Unfortunately, Unity doesn't allow for custom colliders at this time, nor does it allow for non-static concave meshes. Which leaves me pretty limited. However, Unity does allow for convex mesh colliders and even static concave mesh colliders. Creating mesh colliders is very expensive for large meshes though. This led me to experiment with generating very small colliders only around Rigidbodies, at fixed distance intervals.

我还包括了一个非常有趣的示例场景，展示了如何添加物理交互。不幸的是，Unity目前不允许自定义碰撞器，也不允许非静态的凹凸网格。这让我非常受限。然而，Unity允许凸凹网格碰撞器和静态凹凸网格碰撞器。创建大型网格碰撞器非常昂贵。这促使我尝试仅在Rigidbody周围的固定距离间隔内生成非常小的碰撞器。

![blobbyBricks9](https://user-images.githubusercontent.com/18707147/118203358-caa49100-b453-11eb-9d3a-a5af4fff5cca.gif)

It works surprisingly well, even when moving the sdfs around!

它效果出奇地好，即使在移动SDF时也是如此！

## Roadmap and Notes

* ~~I want to be able to add physics to the generated meshes. In theory this should be as simple as adding a MeshCollider and Rigidbody to them, but Unity probably won't play well with these high-poly non-convex meshes, so I may need to split them into many convex meshes.~~
* ~~I intend to add more sdf operations which aren't tied to specific sdf objects, so I can stretch or bend the entire space.~~
* I'd like to figure out how to get the generated 'UV field' to play nicely with seams on SDFMeshes. Currently I just clamp the interpolated UVs if I detect too big a jump between two neighbouring UV samples.
* None of this stuff is particularly cheap on the GPU. I made no special effort to avoid branching and I could probably use less kernels in the mesh generation process.
* ~~Undo is not fully supported in custom editors yet.~~
* ~~Some items, especially SDF meshes, don't always cope nicely with all the different transitions Unity goes to, like entering play mode, or recompiling. I've spent a lot of time improving stability in this regard but it's not yet 100%.~~
* I don't currently use any sort of adaptive octree approach. I consider this a "nice to have."
* I might make a component to automate the "chunking" process, basically just currently positioning the distinct SDFGroupMeshGenerator components, disabling occluded ones, spawning new ones, etc.

![isomesh9](https://user-images.githubusercontent.com/18707147/115975715-03410100-a55f-11eb-8c41-3b983217ba64.gif)

## Why are you using an alpha version of Unity?
~~Note: I left this here for posterity, but Unity has now officially released 2021.2 on the tech stream, so it's no longer in alpha!
You may notice there is an option to switch between 'Procedural' and 'Mesh Filter' output modes. This changes how the mesh data is handed over to Unity for rendering. The 'Mesh Filter' mode simply drags the mesh data back onto the CPU and passes it in to a Mesh Filter component. Procedural mode is waaaay faster - using Unity's DrawProceduralIndirect to keep the data GPU-side. However, you will need a material which is capable of rendering geometry passed in via ComputeBuffers. This project is in URP, which makes it a bit of a pain to hand-write shaders, [and Unity didn't add a VertexID node until ShaderGraph 12, which is only supported by Unity 2021.2.](https://portal.productboard.com/unity/1-unity-platform-rendering-visual-effects/c/258-shader-graph-vertex-id-instance-id-and-eyeid-nodes)~~

~~If you want to try this out, but don't want to use an alpha version of Unity, this stuff is the only difference - you can import this into unity 2020.3, which I was previously working in, and it should be fine except for the procedural output mode and the corresponding shader graph.~~

If you have the amplify shader editor, I've included an amplify custom node class which should let you do the same thing!

## Useful References and Sources

* [Inigo Quilez](https://www.iquilezles.org/)
* [Dual Contouring Tutorial](https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/)
* [Analysis and Acceleration of High Quality Isosurface Contouring](http://www.inf.ufrgs.br/~comba/papers/thesis/diss-leonardo.pdf)
* [Kosmonaut's Signed Distance Field Journey - a fellow SDF mesh creator](https://kosmonautblog.wordpress.com/2017/05/01/signed-distance-field-rendering-journey-pt-1/)
* [DreamCat Games' tutorial on Surface Nets](https://bonsairobo.medium.com/smooth-voxel-mapping-a-technical-deep-dive-on-real-time-surface-nets-and-texturing-ef06d0f8ca14)
* [Local interpolation of surfaces using normal vectors - I use this during the tessellation process to produce smoother geometry](https://stackoverflow.com/questions/25342718/local-interpolation-of-surfaces-using-normal-vectors)
* [Nick's Voxel Blog - good source for learning about implementing the QEF minimizer](http://ngildea.blogspot.com/) [(and their github repo)](https://github.com/nickgildea/qef)
* [MudBun - I came across this tool while already deep in development of this, but it looks awesome and way more clean and professional than this learning exercise.](https://assetstore.unity.com/packages/tools/particles-effects/mudbun-volumetric-vfx-mesh-tool-177891)
