![image](https://github.com/user-attachments/assets/5000b672-d97f-4940-af73-f4a28a9d0b17)
﻿
1.the known perfermance bottleneck,the sorting operation behaves too heavier on every frame,need to port this on compute shader in gpu driven way.
2.for maximizing threads occupancy per wavefront,need to lower down instance count by having per-instance holds as many quads as to get best performance .
3.the already optimized version doesn't included in the repo because of tech patents :(, including GPU sorting millions of quad via Compute Shader ,then rendering via IndirectDraw,  somewhat reducing the overdraw of the quad(trick), adaptive instancing. etc
﻿
﻿
