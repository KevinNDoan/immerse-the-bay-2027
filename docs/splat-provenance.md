# Native splat format references

The relay decoder follows the SPZ v2/v3 format and quaternion decoding described by [Niantic Labs SPZ](https://github.com/nianticlabs/spz). The mobile asset's DC spherical-harmonic color is decoded into RGB. Higher SH bands are not rendered. No splat renderer package or native plug-in was added to Unity.

Projection uses the standard Gaussian covariance projection and symmetric 2×2 eigendecomposition, cross-checked against [Aras Pranckevičius' UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting). Quest submission uses ordinary URP mesh rendering with Unity's stereo macros; sorting is a bounded CPU worker task.

SPZ reference license:

MIT License

Copyright (c) 2025 Niantic Labs
Copyright (c) 2025 Adobe Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
