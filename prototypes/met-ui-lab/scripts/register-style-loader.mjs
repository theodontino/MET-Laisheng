import {register} from 'node:module';
// Server rendering checks inspect React output, not browser layout.
register('./style-loader.mjs', import.meta.url);
