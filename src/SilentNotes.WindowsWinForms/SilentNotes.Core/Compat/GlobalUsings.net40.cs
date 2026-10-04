// C#10 global usings: a few of the newer frozen source files reference Task/Path etc.
// without the corresponding using directives (they postdate the previously prebuilt DLL).
// Declaring them globally keeps the frozen sources untouched.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading.Tasks;
