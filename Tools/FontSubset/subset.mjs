// 갈무리 폰트를 게임에서 실제로 쓰는 글자만 남겨 줄인다(WebGL 데이터 용량 절약).
// 원본(전체 글자)은 Tools/FontSubset/source/에 두고, 결과를 Assets/Resources/Fonts/에 같은 이름으로 덮어쓴다(.meta·GUID는 그대로).
// 번역 시트·JSON·코드 문구를 바꿔 새 글자가 생기면 다시 실행한다. EditMode 테스트(FontCoverageTests)가 빠진 글자를 알려 준다.
//   cd Tools/FontSubset && npm install && node subset.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import subsetFont from 'subset-font';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const fonts = ['Galmuri11.ttf', 'Galmuri14.ttf'];
// 확인용으로 다른 폴더에 쓰고 싶으면 FONT_SRC / FONT_OUT 환경 변수로 바꾼다.
const sourceDir = process.env.FONT_SRC || path.join(here, 'source');
const outDir = process.env.FONT_OUT || path.join(root, 'Assets', 'Resources', 'Fonts');

function walk(dir, test, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (fs.statSync(full).isDirectory()) walk(full, test, out);
    else if (test(full)) out.push(full);
  }
  return out;
}

const chars = new Set();
const add = text => { for (const c of text) chars.add(c); };

// 1) 번역 표·데이터 JSON: 파일 전체 글자.
for (const file of [
  ...walk(path.join(root, 'Assets', 'Resources'), f => /\.(csv|json|txt)$/i.test(f) && !f.includes(path.sep + 'Fonts' + path.sep)),
  ...walk(path.join(root, 'Assets', 'Art'), f => /\.json$/i.test(f)),
]) {
  add(fs.readFileSync(file, 'utf8'));
}

// 2) 코드의 문자열 리터럴(주석은 뺀다).
for (const file of walk(path.join(root, 'Assets', 'Scripts'), f => f.endsWith('.cs'))) {
  const source = fs.readFileSync(file, 'utf8').replace(/\/\/.*$/gm, '');
  for (const match of source.matchAll(/"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)'/g)) add(match[0]);
}

// 3) 씬·프리팹에 구워 둔 Text 글자(\uXXXX로 저장돼 있다).
for (const file of walk(path.join(root, 'Assets'), f => /\.(unity|prefab)$/.test(f))) {
  for (const match of fs.readFileSync(file, 'utf8').matchAll(/m_Text: (.*)/g)) {
    add(match[1].replace(/\\u([0-9A-Fa-f]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16))));
  }
}

// 4) 늘 넣어 두는 글자: ASCII 전체(쿠폰 입력 등), 라틴-1 기호, 자주 쓰는 기호, 한글 자모.
for (let c = 0x20; c <= 0x7e; c++) chars.add(String.fromCharCode(c));
for (let c = 0xa0; c <= 0xff; c++) chars.add(String.fromCharCode(c));
for (let c = 0x3131; c <= 0x318e; c++) chars.add(String.fromCharCode(c));
// 자주 쓰는 한글 2,350자(KS X 1001). 문구를 조금 고쳐도 다시 돌리지 않아도 되게 여유로 넣는다(두 폰트 합쳐 약 1MB).
const euckr = new TextDecoder('euc-kr');
for (let lead = 0xb0; lead <= 0xc8; lead++) {
  for (let trail = 0xa1; trail <= 0xfe; trail++) {
    const c = euckr.decode(Uint8Array.of(lead, trail));
    if (c >= '가' && c <= '힣') chars.add(c);
  }
}
add('·•…–—‘’“”「」『』【】→←↑↓─│━★☆♪♫×÷✓✔※○●◎◇◆□■△▲▽▼◀▶♥♡～￦％！？');
for (const c of [...chars]) if (c === '\n' || c === '\r' || c === '\t') chars.delete(c);

const text = [...chars].join('');
const hangul = [...chars].filter(c => c >= '가' && c <= '힣').length;
console.log(`글자 ${chars.size}개 (한글 음절 ${hangul}개)`);

for (const name of fonts) {
  const source = path.join(sourceDir, name);
  const target = path.join(outDir, name);
  const input = fs.readFileSync(source);
  const output = await subsetFont(input, text, { targetFormat: 'truetype' });
  fs.writeFileSync(target, output);
  console.log(`${name}: ${(input.length / 1024).toFixed(0)}KB → ${(output.length / 1024).toFixed(0)}KB`);
}
