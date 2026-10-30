import * as THREE from "/lib/three/three.module.min.js";

const L = 2;          // stone length
const W = 1;          // stone width
const H = 0.26;       // stone thickness
const LIMITE_X = 8;   // where a chain arm turns
const AREA = { x: 8.8, z: 3.9 }; // board area the chain is scaled to fit
const ROSA = 0xed145b;

const PIPS = {
    0: [],
    1: [[0.5, 0.5]],
    2: [[0.27, 0.27], [0.73, 0.73]],
    3: [[0.25, 0.25], [0.5, 0.5], [0.75, 0.75]],
    4: [[0.27, 0.27], [0.73, 0.27], [0.27, 0.73], [0.73, 0.73]],
    5: [[0.25, 0.25], [0.75, 0.25], [0.5, 0.5], [0.25, 0.75], [0.75, 0.75]],
    6: [[0.27, 0.22], [0.73, 0.22], [0.27, 0.5], [0.73, 0.5], [0.27, 0.78], [0.73, 0.78]]
};

export const chave = (a, b) => `${Math.min(a, b)}-${Math.max(a, b)}`;

function formaArredondada(largura, altura, raio) {
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

function texturaFace(menor, maior) {
    const c = document.createElement("canvas");
    c.width = 512; c.height = 256;
    const g = c.getContext("2d");
    const grad = g.createLinearGradient(0, 0, 0, 256);
    grad.addColorStop(0, "#fbf8f1");
    grad.addColorStop(1, "#ece6d8");
    g.fillStyle = grad;
    g.fillRect(0, 0, 512, 256);

    g.strokeStyle = "#2a2a2e";
    g.lineWidth = 7;
    g.beginPath(); g.moveTo(256, 28); g.lineTo(256, 228); g.stroke();
    g.fillStyle = "#b8945a";
    g.beginPath(); g.arc(256, 128, 11, 0, Math.PI * 2); g.fill();

    const desenhar = (valor, deslocX) => {
        for (const [px, py] of PIPS[valor]) {
            const cx = deslocX + 28 + px * 200, cy = 28 + py * 200;
            const rg = g.createRadialGradient(cx - 4, cy - 4, 2, cx, cy, 20);
            rg.addColorStop(0, "#3a3a40");
            rg.addColorStop(1, "#0d0d10");
            g.fillStyle = rg;
            g.beginPath(); g.arc(cx, cy, 19, 0, Math.PI * 2); g.fill();
        }
    };
    desenhar(menor, 0);
    desenhar(maior, 256);

    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 8;
    return t;
}

function texturaVerso() {
    const c = document.createElement("canvas");
    c.width = 256; c.height = 128;
    const g = c.getContext("2d");
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
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    return t;
}

function texturaFeltro() {
    const c = document.createElement("canvas");
    c.width = 1024; c.height = 640;
    const g = c.getContext("2d");
    const grad = g.createRadialGradient(512, 320, 60, 512, 320, 620);
    grad.addColorStop(0, "#4a2266");
    grad.addColorStop(1, "#1a0b26");
    g.fillStyle = grad;
    g.fillRect(0, 0, 1024, 640);
    const ruido = g.getImageData(0, 0, 1024, 640);
    for (let i = 0; i < ruido.data.length; i += 4) {
        const n = (Math.random() - 0.5) * 14;
        ruido.data[i] += n; ruido.data[i + 1] += n; ruido.data[i + 2] += n;
    }
    g.putImageData(ruido, 0, 0);
    g.strokeStyle = "#ff1f6d";
    g.lineWidth = 8;
    g.beginPath();
    g.roundRect(34, 34, 956, 572, 150);
    g.stroke();
    g.strokeStyle = "rgba(255,201,40,0.55)";
    g.lineWidth = 3;
    g.setLineDash([18, 14]);
    g.beginPath();
    g.roundRect(52, 52, 920, 536, 134);
    g.stroke();
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    return t;
}

/**
 * Lays the chain out from the opening stone outwards. Each arm runs horizontally and,
 * at the table edge, turns with one vertical stone and comes back the other way.
 * Returns a placement per chain index plus where the next stone of each arm would go.
 */
export function layoutCadeia(cadeia, indiceAbertura) {
    const pos = new Array(cadeia.length);
    if (cadeia.length === 0) return { pos, proxima: { esquerda: null, direita: null } };

    const c = Math.min(Math.max(indiceAbertura, 0), cadeia.length - 1);
    const centro = cadeia[c];
    const centroDupla = centro.valorA === centro.valorB;
    pos[c] = { x: 0, z: 0, angulo: 0, inicio: centro.valorA, transversal: centroDupla };
    const meia = (centroDupla ? W : L) / 2;

    const braco = (indices, sentido, virada, inicioDe) => {
        let p = { x: sentido * meia, z: 0 };
        let d = { x: sentido, z: 0 };
        let horizontal = sentido;
        let naCurva = false;

        const virar = (novo) => {
            p = { x: p.x + d.x * W / 2 - novo.x * W / 2, z: p.z + d.z * W / 2 - novo.z * W / 2 };
            d = novo;
        };

        const colocar = (peca) => {
            const dupla = peca && peca.valorA === peca.valorB;
            if (naCurva) {
                horizontal = -horizontal;
                virar({ x: horizontal, z: 0 });
                naCurva = false;
            }
            let comprimento = dupla ? W : L;
            if (d.z === 0 && Math.abs(p.x + d.x * comprimento) > LIMITE_X) {
                virar({ x: 0, z: virada });
                naCurva = true;
                comprimento = L;
            }
            const transversal = dupla && !naCurva;
            const local = {
                x: p.x + d.x * comprimento / 2,
                z: p.z + d.z * comprimento / 2,
                angulo: Math.atan2(-d.z, d.x),
                transversal
            };
            p = { x: p.x + d.x * comprimento, z: p.z + d.z * comprimento };
            return local;
        };

        for (const i of indices) pos[i] = { ...colocar(cadeia[i]), inicio: inicioDe(cadeia[i]) };
        return colocar(null);
    };

    const direita = [];
    for (let i = c + 1; i < cadeia.length; i++) direita.push(i);
    const esquerda = [];
    for (let i = c - 1; i >= 0; i--) esquerda.push(i);

    const proximaDireita = braco(direita, 1, 1, (p) => p.valorA);
    const proximaEsquerda = braco(esquerda, -1, -1, (p) => p.valorB);
    return { pos, proxima: { esquerda: proximaEsquerda, direita: proximaDireita } };
}

export class Cena {
    constructor(canvas, aoClicar) {
        this.canvas = canvas;
        this.aoClicar = aoClicar;
        this.pedras = new Map();     // key -> mesh for every face-up stone
        this.versos = [];            // opponent hand
        this.cava = [];              // boneyard pile
        this.fantasmas = [];         // side choice markers
        this.interativo = false;
        this.hover = null;
        this.texturas = new Map();
        this.relogio = new THREE.Clock();

        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;

        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x12081a);
        this.scene.fog = new THREE.Fog(0x12081a, 30, 60);
        this.camera = new THREE.PerspectiveCamera(38, 1, 0.1, 200);

        this.#montarAmbiente();
        this.#montarGeometrias();

        this.raycaster = new THREE.Raycaster();
        this.ponteiro = new THREE.Vector2();
        canvas.addEventListener("pointermove", (e) => this.#mover(e));
        canvas.addEventListener("pointerdown", (e) => this.#clicar(e));
        new ResizeObserver(() => this.#redimensionar()).observe(canvas);
        this.#redimensionar();
        this.renderer.setAnimationLoop(() => this.#quadro());
    }

    #montarAmbiente() {
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x221a20, 0.9));
        const sol = new THREE.DirectionalLight(0xfff4e6, 2.2);
        sol.position.set(-6, 18, 9);
        sol.castShadow = true;
        sol.shadow.mapSize.set(2048, 2048);
        Object.assign(sol.shadow.camera, { left: -16, right: 16, top: 12, bottom: -12, near: 1, far: 50 });
        sol.shadow.bias = -0.0004;
        this.scene.add(sol);
        const rosa = new THREE.PointLight(ROSA, 40, 30, 2);
        rosa.position.set(12, 5, -8);
        this.scene.add(rosa);

        const feltro = new THREE.Mesh(
            new THREE.PlaneGeometry(26, 16.25),
            new THREE.MeshStandardMaterial({ map: texturaFeltro(), roughness: 0.95 }));
        feltro.rotation.x = -Math.PI / 2;
        feltro.receiveShadow = true;
        this.scene.add(feltro);

        const borda = formaArredondada(28, 18.25, 3.4);
        borda.holes.push(formaArredondada(26, 16.25, 2.6));
        const madeira = new THREE.Mesh(
            new THREE.ExtrudeGeometry(borda, { depth: 0.7, bevelEnabled: true, bevelSize: 0.25, bevelThickness: 0.25, bevelSegments: 4, curveSegments: 24 }),
            new THREE.MeshStandardMaterial({ color: 0x7a3b1c, roughness: 0.35, metalness: 0.15 }));
        madeira.rotation.x = -Math.PI / 2;
        madeira.position.y = -0.35;
        madeira.castShadow = true;
        madeira.receiveShadow = true;
        this.scene.add(madeira);

        const chao = new THREE.Mesh(new THREE.PlaneGeometry(200, 200), new THREE.MeshStandardMaterial({ color: 0x150a1e, roughness: 1 }));
        chao.rotation.x = -Math.PI / 2;
        chao.position.y = -3;
        this.scene.add(chao);
    }

    #montarGeometrias() {
        const corpo = new THREE.ExtrudeGeometry(formaArredondada(L - 0.08, W - 0.08, 0.14),
            { depth: H - 0.08, bevelEnabled: true, bevelSize: 0.04, bevelThickness: 0.04, bevelSegments: 3, curveSegments: 8 });
        corpo.rotateX(-Math.PI / 2);
        corpo.translate(0, 0.04, 0);
        this.geoCorpo = corpo;
        this.geoFace = new THREE.PlaneGeometry(L - 0.2, W - 0.14).rotateX(-Math.PI / 2);
        this.matCorpo = new THREE.MeshStandardMaterial({ color: 0xfff8ea, roughness: 0.3 });
        this.matContorno = new THREE.MeshBasicMaterial({ color: 0x0b0612, side: THREE.BackSide });
        this.matVerso = new THREE.MeshStandardMaterial({ map: texturaVerso(), roughness: 0.5 });
        this.matCorpoEscuro = new THREE.MeshStandardMaterial({ color: 0x2a1838, roughness: 0.45 });
        this.matFantasma = new THREE.MeshBasicMaterial({ color: ROSA, transparent: true, opacity: 0.35, depthWrite: false });
    }

    /** Inverted-hull outline: a slightly bigger back-faced copy drawn in the outline colour. */
    #contorno() {
        const c = new THREE.Mesh(this.geoCorpo, this.matContorno);
        c.scale.set(1.035, 1.15, 1.07);
        c.position.y = -0.015;
        return c;
    }

    #criarPedra(a, b) {
        const k = chave(a, b);
        if (!this.texturas.has(k)) this.texturas.set(k, texturaFace(Math.min(a, b), Math.max(a, b)));
        const grupo = new THREE.Group();
        const corpo = new THREE.Mesh(this.geoCorpo, this.matCorpo);
        corpo.castShadow = true;
        corpo.receiveShadow = true;
        const face = new THREE.Mesh(this.geoFace, new THREE.MeshStandardMaterial({ map: this.texturas.get(k), roughness: 0.4 }));
        face.position.y = H + 0.004;
        grupo.add(corpo, face, this.#contorno());
        grupo.userData = { chave: k, menor: Math.min(a, b), alvo: null, face };
        return grupo;
    }

    #criarVerso() {
        const grupo = new THREE.Group();
        const corpo = new THREE.Mesh(this.geoCorpo, this.matCorpoEscuro);
        corpo.castShadow = true;
        const face = new THREE.Mesh(this.geoFace, this.matVerso);
        face.position.y = H + 0.004;
        grupo.add(corpo, face, this.#contorno());
        grupo.userData = { alvo: null };
        return grupo;
    }

    /** Converts a placement (flat on the table) into a target transform. */
    static #alvoPlano(x, z, angulo, escala, y = 0) {
        return {
            pos: new THREE.Vector3(x, y, z),
            quat: new THREE.Quaternion().setFromEuler(new THREE.Euler(0, angulo, 0)),
            escala
        };
    }

    static #alvoEmPe(x, z, levantar = 0) {
        const quat = new THREE.Quaternion().setFromEuler(new THREE.Euler(0.62, Math.PI / 2, 0, "XYZ"));
        return { pos: new THREE.Vector3(x, 0.55 + levantar, z - levantar * 0.4), quat, escala: 1 };
    }

    /** Rebuilds every target from a confirmed state; meshes then glide there. */
    aplicar(visao, opcoes) {
        const { pos, proxima } = layoutCadeia(visao.cadeia, visao.indiceAbertura);
        const extensao = pos.reduce((m, p) => ({ x: Math.max(m.x, Math.abs(p.x) + 1), z: Math.max(m.z, Math.abs(p.z) + 1) }), { x: 1, z: 1 });
        const escala = Math.min(1, AREA.x / extensao.x, AREA.z / extensao.z);
        this.escala = escala;
        this.proxima = proxima;

        const vistas = new Set();
        visao.cadeia.forEach((peca, i) => {
            const p = pos[i];
            const k = chave(peca.valorA, peca.valorB);
            vistas.add(k);
            let mesh = this.pedras.get(k);
            if (!mesh) {
                mesh = this.#criarPedra(peca.valorA, peca.valorB);
                const origem = this.versos.at(-1)?.position ?? new THREE.Vector3(0, 2, -7.5);
                mesh.position.copy(origem).add(new THREE.Vector3(0, 2.5, 0));
                mesh.quaternion.setFromEuler(new THREE.Euler(0, Math.PI / 2, 0));
                this.scene.add(mesh);
                this.pedras.set(k, mesh);
            }
            const giro = (p.inicio === mesh.userData.menor ? 0 : Math.PI) + (p.transversal ? Math.PI / 2 : 0);
            mesh.userData.alvo = Cena.#alvoPlano(p.x * escala, p.z * escala, p.angulo + giro, escala);
            mesh.userData.naMao = false;
        });

        const n = visao.mao.length;
        const espaco = Math.min(1.25, 17 / Math.max(n, 1));
        visao.mao.forEach((peca, i) => {
            const k = chave(peca.valorA, peca.valorB);
            vistas.add(k);
            let mesh = this.pedras.get(k);
            if (!mesh) {
                mesh = this.#criarPedra(peca.valorA, peca.valorB);
                mesh.position.set(-11, 1, 3);
                this.scene.add(mesh);
                this.pedras.set(k, mesh);
            }
            mesh.userData.alvo = Cena.#alvoEmPe((i - (n - 1) / 2) * espaco, 5.9);
            mesh.userData.naMao = true;
            mesh.userData.indiceMao = i;
        });

        for (const [k, mesh] of this.pedras) {
            if (!vistas.has(k)) {
                this.scene.remove(mesh);
                this.pedras.delete(k);
            }
        }

        this.#sincronizarVersos(visao.oponente?.pedras ?? 0);
        this.#sincronizarCava(visao.cava);
        this.interativo = opcoes.interativo;
        this.esconderFantasmas();
    }

    #sincronizarVersos(qtd) {
        while (this.versos.length > qtd) this.scene.remove(this.versos.pop());
        while (this.versos.length < qtd) {
            const v = this.#criarVerso();
            v.position.set(0, 3, -10);
            this.scene.add(v);
            this.versos.push(v);
        }
        const espaco = Math.min(1.2, 15 / Math.max(qtd, 1));
        this.versos.forEach((v, i) => {
            v.userData.alvo = Cena.#alvoPlano((i - (qtd - 1) / 2) * espaco, -5.5, Math.PI / 2, 0.85);
        });
    }

    #sincronizarCava(qtd) {
        const visiveis = Math.min(qtd, 14);
        while (this.cava.length > visiveis) this.scene.remove(this.cava.pop());
        while (this.cava.length < visiveis) {
            const v = this.#criarVerso();
            v.position.set(-11, 0, -4);
            this.scene.add(v);
            this.cava.push(v);
        }
        this.cava.forEach((v, i) => {
            const camada = Math.floor(i / 4), lugar = i % 4;
            v.userData.alvo = Cena.#alvoPlano(-10.6 + (lugar % 2) * 1.05, -1.2 + Math.floor(lugar / 2) * 2, Math.PI / 2 + (camada % 2) * 0.08, 0.85, camada * H * 0.85);
        });
    }

    /** Shows translucent stones where each side would receive the selected stone. */
    mostrarFantasmas(lados) {
        this.esconderFantasmas();
        for (const lado of lados) {
            const alvo = lado === 0 ? this.proxima?.esquerda : this.proxima?.direita;
            if (!alvo) continue;
            const g = new THREE.Mesh(new THREE.BoxGeometry(L - 0.1, H, W - 0.1), this.matFantasma);
            g.position.set(alvo.x * this.escala, H / 2 + 0.05, alvo.z * this.escala);
            g.rotation.y = alvo.angulo;
            g.scale.setScalar(this.escala);
            g.userData = { lado };
            this.scene.add(g);
            this.fantasmas.push(g);
        }
    }

    esconderFantasmas() {
        for (const f of this.fantasmas) this.scene.remove(f);
        this.fantasmas = [];
    }

    #redimensionar() {
        const { clientWidth: w, clientHeight: h } = this.canvas;
        if (!w || !h) return;
        this.renderer.setSize(w, h, false);
        const aspecto = w / h;
        this.camera.aspect = aspecto;

        // Pull back just enough to keep the play area (chain + boneyard) in view horizontally.
        // Portrait screens (tablets) get a more top-down angle to use their extra height.
        const meiaLarguraVisivel = 11.8;
        const tanHorizontal = Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2)) * aspecto;
        const distancia = Math.max(23.2, meiaLarguraVisivel / tanHorizontal);
        const direcao = aspecto < 1 ? new THREE.Vector3(0, 0.93, 0.37) : new THREE.Vector3(0, 19.5, 12.5);
        this.camera.position.copy(direcao.normalize().multiplyScalar(distancia));
        this.camera.lookAt(0, 0, 0.9);
        this.camera.far = distancia + 80;
        this.camera.updateProjectionMatrix();
        this.scene.fog.near = distancia + 8;
        this.scene.fog.far = distancia + 45;
    }

    #intersectar(e) {
        const r = this.canvas.getBoundingClientRect();
        this.ponteiro.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
        this.raycaster.setFromCamera(this.ponteiro, this.camera);
        if (this.fantasmas.length) {
            const f = this.raycaster.intersectObjects(this.fantasmas, false)[0];
            if (f) return { tipo: "lado", lado: f.object.userData.lado };
        }
        const naMao = [...this.pedras.values()].filter((m) => m.userData.naMao);
        const hit = this.raycaster.intersectObjects(naMao, true)[0];
        if (!hit) return null;
        let obj = hit.object;
        while (obj && !obj.userData.chave) obj = obj.parent;
        return obj ? { tipo: "pedra", chave: obj.userData.chave } : null;
    }

    #mover(e) {
        const alvo = this.interativo ? this.#intersectar(e) : null;
        this.hover = alvo?.tipo === "pedra" ? alvo.chave : null;
        this.canvas.style.cursor = this.hover || alvo?.tipo === "lado" ? "pointer" : "default";
    }

    #clicar(e) {
        if (!this.interativo) return;
        const alvo = this.#intersectar(e);
        if (alvo) this.aoClicar(alvo);
    }

    #quadro() {
        const dt = Math.min(this.relogio.getDelta(), 0.05);
        const f = 1 - Math.pow(0.0008, dt);
        const t = this.relogio.elapsedTime;

        const animar = (mesh) => {
            const alvo = mesh.userData.alvo;
            if (!alvo) return;
            let pos = alvo.pos;
            if (mesh.userData.naMao && mesh.userData.chave === this.hover) pos = pos.clone().add(new THREE.Vector3(0, 0.45, -0.25));
            mesh.position.lerp(pos, f);
            mesh.quaternion.slerp(alvo.quat, f);
            const s = THREE.MathUtils.lerp(mesh.scale.x, alvo.escala, f);
            mesh.scale.setScalar(s);
        };
        for (const m of this.pedras.values()) animar(m);
        for (const m of this.versos) animar(m);
        for (const m of this.cava) animar(m);

        this.matFantasma.opacity = 0.3 + Math.sin(t * 5) * 0.15;
        this.renderer.render(this.scene, this.camera);
    }
}
