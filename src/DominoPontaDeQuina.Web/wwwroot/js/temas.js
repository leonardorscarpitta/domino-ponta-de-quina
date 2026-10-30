import * as THREE from "/lib/three/three.module.min.js";

// Each theme builds everything around the play area: lights, table, floor and props.
// The play area itself (stones, hand, boneyard) is the same for every theme and
// lives inside x ±11.8, z ±7, so props stay outside it.

export const TEMA_PADRAO = "poker";

export function formaArredondada(largura, altura, raio) {
    const s = new THREE.Shape();
    const x = -largura / 2, y = -altura / 2;
    s.moveTo(x + raio, y);
    s.lineTo(x + largura - raio, y);
    s.quadraticCurveTo(x + largura, y, x + largura, y + raio);
    s.lineTo(x + largura, y + altura - raio);
    s.quadraticCurveTo(x + largura, y + altura, x + largura - raio, y + altura);
    s.lineTo(x + raio, y + altura);
    s.quadraticCurveTo(x, y + altura, x, y + altura - raio);
    s.lineTo(x, y + raio);
    s.quadraticCurveTo(x, y, x + raio, y);
    return s;
}

function canvas(w, h, desenhar) {
    const c = document.createElement("canvas");
    c.width = w; c.height = h;
    desenhar(c.getContext("2d"), w, h);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 8;
    return t;
}

function ruido(g, w, h, intensidade) {
    const img = g.getImageData(0, 0, w, h);
    for (let i = 0; i < img.data.length; i += 4) {
        const n = (Math.random() - 0.5) * intensidade;
        img.data[i] += n; img.data[i + 1] += n; img.data[i + 2] += n;
    }
    g.putImageData(img, 0, 0);
}

function luzes(grupo, { ceu, chao, intensidade, corSol, sol }) {
    grupo.add(new THREE.HemisphereLight(ceu, chao, intensidade));
    const luz = new THREE.DirectionalLight(corSol, sol);
    luz.position.set(-6, 18, 9);
    luz.castShadow = true;
    luz.shadow.mapSize.set(2048, 2048);
    Object.assign(luz.shadow.camera, { left: -16, right: 16, top: 12, bottom: -12, near: 1, far: 50 });
    luz.shadow.bias = -0.0004;
    grupo.add(luz);
}

function piso(grupo, material) {
    const chao = new THREE.Mesh(new THREE.PlaneGeometry(200, 200), material);
    chao.rotation.x = -Math.PI / 2;
    chao.position.y = -3;
    chao.receiveShadow = true;
    grupo.add(chao);
}

/* ───────────── Mesa de Poker ───────────── */

const poker = {
    nome: "Mesa de Poker",
    fundo: 0x12081a,
    fantasma: 0xff1f6d,
    corpoVerso: 0x2a1838,
    verso: () => canvas(256, 128, (g) => {
        g.fillStyle = "#1d1d22";
        g.fillRect(0, 0, 256, 128);
        g.strokeStyle = "rgba(255,255,255,0.07)";
        g.lineWidth = 2;
        for (let i = -128; i < 256; i += 14) {
            g.beginPath(); g.moveTo(i, 0); g.lineTo(i + 128, 128); g.stroke();
            g.beginPath(); g.moveTo(i + 128, 0); g.lineTo(i, 128); g.stroke();
        }
        g.strokeStyle = "rgba(237,20,91,0.55)";
        g.lineWidth = 4;
        g.strokeRect(8, 8, 240, 112);
    }),
    montar(grupo) {
        luzes(grupo, { ceu: 0xffffff, chao: 0x221a20, intensidade: 0.9, corSol: 0xfff4e6, sol: 2.2 });
        const rosa = new THREE.PointLight(0xed145b, 40, 30, 2);
        rosa.position.set(12, 5, -8);
        grupo.add(rosa);

        const feltro = new THREE.Mesh(
            new THREE.PlaneGeometry(26, 16.25),
            new THREE.MeshStandardMaterial({
                roughness: 0.95,
                map: canvas(1024, 640, (g, w, h) => {
                    const grad = g.createRadialGradient(w / 2, h / 2, 60, w / 2, h / 2, 620);
                    grad.addColorStop(0, "#4a2266");
                    grad.addColorStop(1, "#1a0b26");
                    g.fillStyle = grad;
                    g.fillRect(0, 0, w, h);
                    ruido(g, w, h, 14);
                    g.strokeStyle = "#ff1f6d";
                    g.lineWidth = 8;
                    g.beginPath(); g.roundRect(34, 34, 956, 572, 150); g.stroke();
                    g.strokeStyle = "rgba(255,201,40,0.55)";
                    g.lineWidth = 3;
                    g.setLineDash([18, 14]);
                    g.beginPath(); g.roundRect(52, 52, 920, 536, 134); g.stroke();
                })
            }));
        feltro.rotation.x = -Math.PI / 2;
        feltro.receiveShadow = true;
        grupo.add(feltro);

        const borda = formaArredondada(28, 18.25, 3.4);
        borda.holes.push(formaArredondada(26, 16.25, 2.6));
        const madeira = new THREE.Mesh(
            new THREE.ExtrudeGeometry(borda, { depth: 0.7, bevelEnabled: true, bevelSize: 0.25, bevelThickness: 0.25, bevelSegments: 4, curveSegments: 24 }),
            new THREE.MeshStandardMaterial({ color: 0x7a3b1c, roughness: 0.35, metalness: 0.15 }));
        madeira.rotation.x = -Math.PI / 2;
        madeira.position.y = -0.35;
        madeira.castShadow = true;
        madeira.receiveShadow = true;
        grupo.add(madeira);

        piso(grupo, new THREE.MeshStandardMaterial({ color: 0x150a1e, roughness: 1 }));
    }
};

/* ───────────── Mesa de Bar (boteco) ───────────── */

const AMARELO = "#ffc400";

function texturaTampoBar(lado) {
    return canvas(1024, 1280, (g, w, h) => {
        const grad = g.createRadialGradient(w * 0.5, h * 0.45, 80, w * 0.5, h * 0.5, 900);
        grad.addColorStop(0, "#ffd84a");
        grad.addColorStop(1, "#f2b100");
        g.fillStyle = grad;
        g.fillRect(0, 0, w, h);
        ruido(g, w, h, 6);

        // Moulded rim of the plastic table
        g.strokeStyle = "rgba(160,100,0,0.35)";
        g.lineWidth = 18;
        g.beginPath(); g.roundRect(30, 30, w - 60, h - 60, 70); g.stroke();
        g.strokeStyle = "rgba(255,255,255,0.35)";
        g.lineWidth = 5;
        g.beginPath(); g.roundRect(48, 48, w - 96, h - 96, 58); g.stroke();

        // Made-up brewery print, once per table, near the far outer corner
        const cx = lado < 0 ? 300 : w - 300, cy = 250;
        g.save();
        g.translate(cx, cy);
        g.rotate(lado < 0 ? -0.12 : 0.12);
        g.fillStyle = "#d62718";
        g.beginPath(); g.arc(0, 0, 150, 0, Math.PI * 2); g.fill();
        g.strokeStyle = "#fff";
        g.lineWidth = 10;
        g.beginPath(); g.arc(0, 0, 128, 0, Math.PI * 2); g.stroke();
        g.fillStyle = "#fff";
        g.textAlign = "center";
        g.font = "bold 46px 'Lilita One', sans-serif";
        g.fillText("BAR DO", 0, -18);
        g.font = "bold 64px 'Lilita One', sans-serif";
        g.fillText("QUINA", 0, 46);
        g.font = "bold 24px 'Nunito', sans-serif";
        g.fillText("CERVEJA GELADA", 0, 92);
        g.restore();
    });
}

function texturaCalcadao() {
    // Black-and-white wave pavement, like the Copacabana promenade
    const t = canvas(512, 512, (g, w, h) => {
        g.fillStyle = "#e9e4d8";
        g.fillRect(0, 0, w, h);
        g.fillStyle = "#1c1c1c";
        const faixa = 128;
        for (let y0 = -faixa; y0 < h + faixa; y0 += faixa) {
            g.beginPath();
            g.moveTo(0, y0);
            for (let x = 0; x <= w; x += 4) g.lineTo(x, y0 + Math.sin((x / w) * Math.PI * 2) * 34);
            for (let x = w; x >= 0; x -= 4) g.lineTo(x, y0 + faixa / 2 + Math.sin((x / w) * Math.PI * 2) * 34);
            g.closePath();
            g.fill();
        }
        // Mosaic grout
        g.strokeStyle = "rgba(0,0,0,0.18)";
        g.lineWidth = 1;
        for (let i = 0; i < 900; i++) {
            const x = Math.random() * w, y = Math.random() * h, r = 6 + Math.random() * 6;
            g.strokeRect(x, y, r, r);
        }
    });
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(24, 24);
    return t;
}

function texturaRotulo() {
    return canvas(512, 256, (g, w, h) => {
        g.fillStyle = "#f4ead2";
        g.fillRect(0, 0, w, h);
        g.fillStyle = "#c8201a";
        g.fillRect(0, 26, w, 34);
        g.fillRect(0, h - 60, w, 34);
        g.fillStyle = "#1f3a8a";
        g.textAlign = "center";
        g.font = "bold 70px 'Lilita One', sans-serif";
        for (const x of [w * 0.25, w * 0.75]) {
            g.fillText("QUINA", x, 150);
            g.font = "bold 26px 'Nunito', sans-serif";
            g.fillText("CERVEJA PILSEN", x, 186);
            g.font = "bold 70px 'Lilita One', sans-serif";
        }
    });
}

function garrafa(materiais) {
    // 600 ml bar bottle, stylised (about 1.6x a stone's length)
    const perfil = [
        [0, 0], [0.5, 0], [0.55, 0.06], [0.55, 1.9], [0.5, 2.15], [0.3, 2.5], [0.19, 2.75],
        [0.19, 3.15], [0.22, 3.2], [0.22, 3.28], [0, 3.28]
    ].map(([r, y]) => new THREE.Vector2(r, y));
    const g = new THREE.Group();
    const vidro = new THREE.Mesh(new THREE.LatheGeometry(perfil, 28), materiais.ambar);
    vidro.castShadow = true;
    const rotulo = new THREE.Mesh(new THREE.CylinderGeometry(0.565, 0.565, 0.9, 28, 1, true), materiais.rotulo);
    rotulo.position.y = 1.05;
    const tampa = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 0.12, 18), materiais.tampa);
    tampa.position.y = 3.33;
    g.add(vidro, rotulo, tampa);
    return g;
}

function copoAmericano(materiais, cheio = 0.75) {
    // The faceted bar glass; 12 radial segments give the facets
    const g = new THREE.Group();
    const copo = new THREE.Mesh(new THREE.CylinderGeometry(0.45, 0.36, 1.25, 12, 1, true), materiais.vidro);
    copo.position.y = 0.625;
    const fundo = new THREE.Mesh(new THREE.CylinderGeometry(0.36, 0.36, 0.1, 12), materiais.vidro);
    fundo.position.y = 0.05;
    const alturaCerveja = 1.05 * cheio;
    const cerveja = new THREE.Mesh(new THREE.CylinderGeometry(0.41, 0.35, alturaCerveja, 12), materiais.cerveja);
    cerveja.position.y = 0.1 + alturaCerveja / 2;
    const espuma = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.41, 0.2, 12), materiais.espuma);
    espuma.position.y = 0.1 + alturaCerveja + 0.1;
    g.add(fundo, cerveja, espuma, copo);
    g.traverse((m) => { if (m.isMesh) m.castShadow = true; });
    return g;
}

function pratoDeBolinhos(materiais) {
    const g = new THREE.Group();
    const prato = new THREE.Mesh(new THREE.CylinderGeometry(1.25, 1.0, 0.14, 32), materiais.prato);
    prato.position.y = 0.07;
    prato.receiveShadow = true;
    g.add(prato);
    const bolinho = new THREE.SphereGeometry(0.26, 16, 12);
    const posicoes = [[0, 0], [0.5, 0.2], [-0.45, 0.3], [0.2, -0.5], [-0.35, -0.4], [0.6, -0.35], [-0.1, 0.6], [0.05, 0.05]];
    posicoes.forEach(([x, z], i) => {
        const b = new THREE.Mesh(bolinho, materiais.bolinho);
        b.position.set(x, i === posicoes.length - 1 ? 0.6 : 0.36, z);
        b.scale.set(1, 0.85, 1.15);
        b.rotation.y = i;
        b.castShadow = true;
        g.add(b);
    });
    const palito = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 0.9, 6), materiais.palito);
    palito.position.set(0.05, 0.85, 0.05);
    palito.rotation.z = 0.3;
    g.add(palito);
    return g;
}

function tampinha(materiais) {
    const t = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.26, 0.08, 21).translate(0, 0.04, 0), materiais.tampa);
    t.castShadow = true;
    return t;
}

const bar = {
    nome: "Mesa de Bar",
    fundo: 0x0c1a2e,
    fantasma: 0x00b04f,
    corpoVerso: 0x0a5a2c,
    verso: () => canvas(256, 128, (g, w, h) => {
        g.fillStyle = "#0a7a3a";
        g.fillRect(0, 0, w, h);
        g.fillStyle = AMARELO;
        g.beginPath(); g.moveTo(w / 2, 14); g.lineTo(w - 22, h / 2); g.lineTo(w / 2, h - 14); g.lineTo(22, h / 2); g.closePath(); g.fill();
        g.fillStyle = "#1f3a8a";
        g.beginPath(); g.arc(w / 2, h / 2, 30, 0, Math.PI * 2); g.fill();
        g.strokeStyle = "#fff";
        g.lineWidth = 4;
        g.beginPath(); g.arc(w / 2, h / 2 + 34, 44, Math.PI * 1.25, Math.PI * 1.75); g.stroke();
    }),
    montar(grupo) {
        luzes(grupo, { ceu: 0xfff1d6, chao: 0x1b2a3d, intensidade: 1.0, corSol: 0xffe2b0, sol: 2.0 });
        // Warm bulb hanging over the table
        const lampada = new THREE.PointLight(0xffb347, 55, 34, 2);
        lampada.position.set(-3, 9, -2);
        grupo.add(lampada);

        // Two plastic tables pushed together, as every boteco does
        for (const lado of [-1, 1]) {
            const tampo = new THREE.Mesh(
                new THREE.PlaneGeometry(13, 16.25),
                new THREE.MeshStandardMaterial({ map: texturaTampoBar(lado), roughness: 0.42 }));
            tampo.rotation.x = -Math.PI / 2;
            tampo.position.x = lado * 6.55;
            tampo.receiveShadow = true;
            grupo.add(tampo);

            const corpo = new THREE.Mesh(
                new THREE.ExtrudeGeometry(formaArredondada(13.1, 16.4, 1.4), { depth: 0.5, bevelEnabled: true, bevelSize: 0.3, bevelThickness: 0.3, bevelSegments: 5, curveSegments: 16 }),
                new THREE.MeshStandardMaterial({ color: 0xf2b100, roughness: 0.4 }));
            corpo.rotation.x = -Math.PI / 2;
            corpo.position.set(lado * 6.55, -0.82, 0);
            corpo.castShadow = true;
            corpo.receiveShadow = true;
            grupo.add(corpo);
        }

        piso(grupo, new THREE.MeshStandardMaterial({ map: texturaCalcadao(), roughness: 0.9 }));

        const materiais = {
            ambar: new THREE.MeshStandardMaterial({ color: 0x6b3008, roughness: 0.12, metalness: 0.1, transparent: true, opacity: 0.93 }),
            rotulo: new THREE.MeshStandardMaterial({ map: texturaRotulo(), roughness: 0.6 }),
            tampa: new THREE.MeshStandardMaterial({ color: 0xf2c230, roughness: 0.35, metalness: 0.45, emissive: 0x3a2800, emissiveIntensity: 0.4 }),
            vidro: new THREE.MeshStandardMaterial({ color: 0xdff3ff, roughness: 0.05, transparent: true, opacity: 0.28, side: THREE.DoubleSide, depthWrite: false }),
            cerveja: new THREE.MeshStandardMaterial({ color: 0xf5a516, roughness: 0.2, emissive: 0x5a2c00, emissiveIntensity: 0.25 }),
            espuma: new THREE.MeshStandardMaterial({ color: 0xfffbf0, roughness: 0.9 }),
            prato: new THREE.MeshStandardMaterial({ color: 0xfafafa, roughness: 0.35 }),
            bolinho: new THREE.MeshStandardMaterial({ color: 0xc8741c, roughness: 0.75 }),
            palito: new THREE.MeshStandardMaterial({ color: 0xe8d3a0, roughness: 0.8 })
        };

        const colocar = (obj, x, z, giro = 0) => { obj.position.set(x, 0, z); obj.rotation.y = giro; grupo.add(obj); };
        // Spots the HUD doesn't cover: beside the opponent's row and the far logos, and the near corners.
        colocar(garrafa(materiais), -6.6, -6.7, 0.6);
        colocar(copoAmericano(materiais, 0.8), -5.3, -7.1);
        colocar(garrafa(materiais), 6.7, -6.8, 2.1);
        colocar(copoAmericano(materiais, 0.55), 7.9, -6.9);
        colocar(pratoDeBolinhos(materiais), 8.6, 5.7);
        colocar(copoAmericano(materiais, 0.35), -8.4, 6.1);
        colocar(tampinha(materiais), 6.3, 4.9);
        colocar(tampinha(materiais), -7.2, 4.7);
        const deitada = tampinha(materiais);
        deitada.rotation.z = Math.PI;
        colocar(deitada, -4.4, -6.3);
    }
};

export const TEMAS = { poker, bar };

export function obterTema(id) {
    return TEMAS[id] ?? TEMAS[TEMA_PADRAO];
}
