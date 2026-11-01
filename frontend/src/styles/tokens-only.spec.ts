import { readdirSync, readFileSync } from 'node:fs';

// The token-only rule (contracts/ui.md, "Visual baseline"; research R24): a component's CSS takes its colours and
// font sizes from the tokens of tokens.css, so the two themes and the type scale stay in one place.
const namedColours = new Set(
  `aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue blueviolet brown burlywood
  cadetblue chartreuse chocolate coral cornflowerblue cornsilk crimson cyan darkblue darkcyan darkgoldenrod darkgray
  darkgreen darkgrey darkkhaki darkmagenta darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen
  darkslateblue darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray dimgrey dodgerblue
  firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite gold goldenrod gray green greenyellow grey honeydew
  hotpink indianred indigo ivory khaki lavender lavenderblush lawngreen lemonchiffon lightblue lightcoral lightcyan
  lightgoldenrodyellow lightgray lightgreen lightgrey lightpink lightsalmon lightseagreen lightskyblue lightslategray
  lightslategrey lightsteelblue lightyellow lime limegreen linen magenta maroon mediumaquamarine mediumblue
  mediumorchid mediumpurple mediumseagreen mediumslateblue mediumspringgreen mediumturquoise mediumvioletred
  midnightblue mintcream mistyrose moccasin navajowhite navy oldlace olive olivedrab orange orangered orchid
  palegoldenrod palegreen paleturquoise palevioletred papayawhip peachpuff peru pink plum powderblue purple
  rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown seagreen seashell sienna silver skyblue slateblue
  slategray slategrey snow springgreen steelblue tan teal thistle tomato turquoise violet wheat white whitesmoke yellow
  yellowgreen`.split(/\s+/),
);

function holdsLiteral(property: string, value: string): boolean {
  const own = value.replace(/var\(--[\w-]+\)/g, '').toLowerCase(); // a token's name is not a literal
  const colour =
    /#[0-9a-f]{3,8}\b/.test(own) || /\b(rgba?|hsla?)\(/.test(own) || own.split(/[^a-z-]+/).some((word) => namedColours.has(word));
  const pixelFontSize = /^font(-size)?$/.test(property.toLowerCase()) && /\d(\.\d+)?px\b/.test(own);
  return colour || pixelFontSize;
}

/** The declarations of `css` that hold a colour literal or a font size in px. */
function literals(css: string): string[] {
  const blocks = [...css.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/\{([^{}]*)\}/g)].map(([, block]) => block);
  return blocks
    .flatMap((block) => block.split(';'))
    .map((declaration) => declaration.trim())
    .filter((declaration) => {
      const colon = declaration.indexOf(':');
      return colon > 0 && holdsLiteral(declaration.slice(0, colon).trim(), declaration.slice(colon + 1));
    });
}

describe('component styles', () => {
  it('component styles use only tokens', () => {
    const files = readdirSync('src/app', { recursive: true }).filter((file) => file.endsWith('.css'));
    expect(files, 'no component CSS file found under src/app').not.toEqual([]);

    const offenders = files.flatMap((file) => literals(readFileSync(`src/app/${file}`, 'utf8')).map((found) => `${file}: ${found}`));

    expect(offenders).toEqual([]);
  });

  it('the check catches a literal', () => {
    const sample = `
      .a { color: red; }
      .b { background: #fff; border: 1px solid rgb(0 0 0); outline-color: hsl(120 50% 50%); }
      @media (min-width: 40rem) { .c { font-size: 14px; } }
      .d { color: var(--text); border: 1px solid var(--border); font-size: var(--text-sm); padding: 4px; }
    `;

    expect(literals(sample)).toEqual([
      'color: red',
      'background: #fff',
      'border: 1px solid rgb(0 0 0)',
      'outline-color: hsl(120 50% 50%)',
      'font-size: 14px',
    ]);
  });
});
