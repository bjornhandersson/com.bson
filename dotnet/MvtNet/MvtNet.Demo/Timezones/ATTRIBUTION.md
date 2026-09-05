# Timezone data attribution

`timezones.geojson` comes from [treyerl/timezones](https://github.com/treyerl/timezones),
a simplified world timezone map in GeoJSON form. The `zone` property, a numeric
UTC offset used by the demo for colouring, was derived from the upstream `name`
property.

That map is itself built from [Eric Muller's time zone map](http://efele.net/maps/tz/world/),
the CIA world factbook standard time zones, and the date line from
[Natural Earth](http://www.naturalearthdata.com/downloads/110m-physical-vectors/110m-geographic-lines/).
IANA zones are merged by UTC offset and maritime territorial borders are ignored,
so it is a demo dataset rather than an authoritative one.

## License

The upstream project is MIT licensed, and its notice is reproduced here as that
license requires.

```
MIT License

Copyright (c) 2016 Lukas Treyer

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
```
