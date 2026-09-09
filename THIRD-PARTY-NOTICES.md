# Third-Party Notices

Comfy Capy Calories includes third-party software, scaffolded code, and data integrations. Those materials are not covered by the proprietary terms for the project's original materials in [LICENSE.md](LICENSE.md); each remains subject to its own copyright notice and license.

This document identifies the principal third-party material present in the repository. Copyright headers and license files included with a component are authoritative and must be preserved.

## Tracked browser libraries

| Component | License information in this repository |
| --- | --- |
| Bootstrap 5.3.3 | MIT; see its [LICENSE](CalorieTracker/wwwroot/lib/bootstrap/LICENSE) |
| jQuery 3.7.1 | MIT; see its [LICENSE.txt](CalorieTracker/wwwroot/lib/jquery/LICENSE.txt) |
| jQuery Validation | MIT; see its [LICENSE.md](CalorieTracker/wwwroot/lib/jquery-validation/LICENSE.md) |
| jQuery Validation Unobtrusive 4.0.0 | See the licensing note below and its [LICENSE.txt](CalorieTracker/wwwroot/lib/jquery-validation-unobtrusive/LICENSE.txt) |
| QRCode.js | MIT; see its [LICENSE](CalorieTracker/wwwroot/lib/qrcodejs/LICENSE) |

The vendored jQuery Validation Unobtrusive 4.0.0 `LICENSE.txt` contains the MIT License, consistent with the [upstream v4.0.0 release notes](https://github.com/aspnet/jquery-validation-unobtrusive/releases/tag/v4.0.0), which state that the license was updated to MIT. The generated `jquery.validate.unobtrusive.js` and `jquery.validate.unobtrusive.min.js` files nevertheless retain a header saying they are licensed under Apache License 2.0. Both notices came from the upstream distribution and are preserved unchanged. This document does not rewrite or resolve that discrepancy: retain the MIT license file and the embedded generated-file headers, and consult the upstream project if separate redistribution requires a definitive interpretation.

The vendored jQuery Validation `additional-methods.js` also contains component-specific notices, including an Apache-2.0 notice for code derived from a modified Castle credit-card validator. Those embedded notices must remain with the file.

## ASP.NET Core Identity scaffolded code

Files scaffolded from ASP.NET Core Identity under `CalorieTracker/Areas/Identity` retain .NET Foundation headers stating that those files are licensed under the MIT License. Those headers and the rights they grant apply to the identified scaffolded files; they do not license the entire Comfy Capy project under MIT.

## .NET and NuGet dependencies

The project directly references Microsoft ASP.NET Core Identity, Entity Framework Core, and SQLite provider packages, plus the Resend .NET package. Their package metadata identifies them as MIT-licensed. NuGet also restores transitive dependencies, each under its package-specific terms. The package notices and license metadata supplied by NuGet and the respective upstream projects remain controlling.

The direct package references and exact versions are recorded in [`CalorieTracker.csproj`](CalorieTracker/CalorieTracker.csproj). Build-only packages marked with `PrivateAssets="all"` are not represented as proprietary Comfy Capy code merely because they are used to build the application.

## React, Vite, and the generated browser bundle

The React client source and dependency lockfile are tracked under `CalorieTracker/ClientApp`; `node_modules` is not tracked. [`package-lock.json`](CalorieTracker/ClientApp/package-lock.json) is the component and version inventory for a reproducible install.

The committed production bundle under `CalorieTracker/wwwroot/react-food-search` contains generated third-party runtime code, including React, React DOM, Scheduler, and Vite-generated runtime helper code where present. React, React DOM, and Scheduler are copyright Meta Platforms, Inc. and affiliates and are provided under the MIT License. Vite is copyright 2019-present VoidZero Inc. and Vite contributors and is provided under the MIT License.

The lockfile also records development and build dependencies under MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, BlueOak-1.0.0, CC-BY-4.0, ISC, and MPL-2.0 terms. Not every package in the lockfile is shipped in the browser bundle. The license obligations for the components actually used or distributed still apply.

When redistributing the generated bundle, distribute this notice with it and retain or reproduce all applicable package copyright and license notices. When dependencies or the bundle are regenerated, review the resulting lockfile and bundle and update this notice or an equivalent generated license report so it reflects the code actually distributed.

The MIT terms applicable to the bundled React-family and Vite runtime material are reproduced here because their `node_modules` license files are not tracked:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## USDA FoodData Central

The application can query the USDA FoodData Central API. The repository does not claim ownership of USDA data. The [FoodData Central API Guide](https://fdc.nal.usda.gov/api-guide/) states that FoodData Central data are public domain, are published under CC0 1.0, and may be used without permission. USDA requests that FoodData Central be listed as the source; its suggested citation is:

> U.S. Department of Agriculture, Agricultural Research Service. FoodData Central, 2019. fdc.nal.usda.gov.

This data status is separate from the copyright status of the original Comfy Capy application and artwork.

## No endorsement or transfer of ownership

Third-party names are used only to identify the relevant components or data sources. Their inclusion does not imply endorsement, and the Comfy Capy proprietary notice does not transfer ownership of or impose additional restrictions on them.
