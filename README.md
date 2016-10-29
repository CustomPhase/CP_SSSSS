# Custom Phase Screen-Space Subsurface Scattering
Naive screen-space subsurface scattering solution for Unity 5.<br><br>
<img src="http://customphase.ru/download/img/CP_SSSSS_1.PNG" alt="In action" width="360"/> <br>
Animated gif: http://imgur.com/Nc8VyDv

<h3>Tested only in Unity 5.4.2, works with deferred/forward, gamma/linear, HDR/LDR, DX11/DX9.</h3>
<h3>Scattering distance depends on main camera's near and far clip plane, if you are having problems try adjusting those.</h3>

<h1>How to use:</h1>
<ol>
<li>Put the files into any folder in your .../Assets/Resources folder</li>
<li>Attach the CP_SSSSS_Main script to your main camera</li>
<li>Attach CP_SSSSS_Object script to any Renderer object that you want to have subsurface scattering on</li>
<li>Start the game to see the effect in action</li>
</ol>

<h1>Basic idea behind algorithm:</h1>
<ol>
<li>Blur the source image separably, based on the distance from the camera, and attenuate surrounding sample's influence based on the depth difference between this sample and the center sample (Soft Depth Bias parameter controls the maximum depth difference allowed)</li>
<li>Render the scene with replaced shader, using the mask set in CP_SSSSS_Object script multiplied by the subsurface color</li>
<li>Composite the blurred stuff on top of the original, multiplying it by mask from step 2, and substracting the original based on the Affect Direct parameter</li>
</ol>

<hr>
MIT License

Copyright (c) 2016 Evgeny Erzutov

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
