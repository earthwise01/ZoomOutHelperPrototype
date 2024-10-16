# FunctionalZoomOut / Zoom Out Helper Prototype
work in progress attempt at making zoom out functional in celeste

likely going to probably be mostly obsolete now due to the existence of [extended camera dynamics](https://github.com/Ikersfletch/ExCameraDynamics/tree/main), but idk i'm releasing the code anyway just for the sake of it

comparing them there's a few probably weird decisions i made here, like how i stored the zoom level (2 static fields + with dynamicdata in the level???? hello?? why didnt i just use level.zoom that literally already exists), using a seperate shader for distortion, whatever hookhelper is supposed to be, and in general its kinda messy in places. it still mostly worked pretty well though for what i wanted so i mean ill take that at least