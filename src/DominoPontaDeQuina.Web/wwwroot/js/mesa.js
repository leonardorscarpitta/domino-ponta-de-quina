import { Cena, chave } from "./cena.js";

// Enum values mirror the API contract (numeric JSON enums).
const Situacao = { AguardandoInicio: 0, EmAndamento: 1, Encerrada: 2 };
const Acao = { Aguardar: 0, JogarAbertura: 1, Jogar: 2, Comprar: 3, Passar: 4, Encerrar: 5 };
const INTERVALO_CONSULTA = 1500;
const INTERVALO_OCULTO = 5000;

const raiz = document.getElementById("mesa");
const partidaId = raiz.dataset.partida;
const csrf = document.querySelector('meta[name="csrf-token"]').content;
const $ = (id) => document.getElementById(id);
const campo = (nome) => raiz.querySelector(`[data-campo="${nome}"]`);

let visao = JSON.parse(raiz.dataset.estado);
let enviando = false;
let selecionada = null;
let inicioVez = null;
let timerMensagem = null;

const cena = new Cena($("cena"), aoClicarCena);

function uuid() {
    if (crypto.randomUUID) return crypto.randomUUID();
    const b = crypto.getRandomValues(new Uint8Array(16));
    b[6] = (b[6] & 0x0f) | 0x40;
    b[8] = (b[8] & 0x3f) | 0x80;
    const h = [...b].map((x) => x.toString(16).padStart(2, "0")).join("");
    return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

function mensagem(texto, tipo = "info", duracao = 3500) {
    const el = $("mensagem");
    el.textContent = texto;
    el.className = `hud-mensagem visivel ${tipo}`;
    clearTimeout(timerMensagem);
    timerMensagem = setTimeout(() => el.classList.remove("visivel"), duracao);
}

function controlesAtivos() {
    return visao.minhaVez && !visao.autonomo && !enviando;
}

function render(anterior) {
    const ativo = controlesAtivos();
    cena.aplicar(visao, {
        jogaveis: ativo ? visao.jogadas.map((j) => chave(j.valorA, j.valorB)) : [],
        interativo: ativo
    });

    const eu = visao.eu, op = visao.oponente;
    campo("eu-nome").textContent = eu?.nome ?? "Você";
    campo("eu-pontos").textContent = eu?.pontuacao ?? 0;
    campo("oponente-nome").textContent = op?.nome ?? "Aguardando jogador";
    campo("oponente-pontos").textContent = op?.pontuacao ?? 0;
    campo("oponente-pedras").textContent = `${op?.pedras ?? 0} pedras`;
    campo("rodada").textContent = `Rodada ${visao.rodada}`;
    campo("cava").textContent = `Cava: ${visao.cava}`;
    campo("versao").textContent = `v${visao.versao}`;
    $("hud-eu").classList.toggle("ativo", !!eu?.ehVez);
    $("hud-oponente").classList.toggle("ativo", !!op?.ehVez);

    $("vez").hidden = !visao.minhaVez || visao.autonomo;
    $("btn-comprar").disabled = !(ativo && visao.proximaAcao === Acao.Comprar);
    $("btn-passar").disabled = !(ativo && visao.proximaAcao === Acao.Passar);
    $("autonomo").checked = visao.autonomo;

    $("espera").hidden = visao.situacao !== Situacao.AguardandoInicio;
    campo("espera-titulo").textContent = op ? "Mesa completa" : "Aguardando jogador";
    $("btn-iniciar").disabled = !op || enviando;

    $("fim").hidden = visao.situacao !== Situacao.Encerrada;
    if (visao.situacao === Situacao.Encerrada) {
        const venci = visao.vencedorId && visao.vencedorId === eu?.jogadorId;
        campo("fim-titulo").textContent = visao.vencedorId ? (venci ? "Você venceu! 🏆" : `Vitória de ${op?.nome ?? "oponente"}`) : "Partida encerrada";
        campo("fim-placar").textContent = `Você ${eu?.pontuacao ?? 0} × ${op?.pontuacao ?? 0} ${op?.nome ?? ""}`;
    }

    const lista = $("historico");
    lista.replaceChildren(...visao.eventos.map((e) => {
        const li = document.createElement("li");
        if (e.souEu) li.className = "meu";
        const b = document.createElement("b");
        b.textContent = e.jogador;
        li.append(b, ` ${e.texto} `);
        if (e.pontos > 0) {
            const em = document.createElement("em");
            em.textContent = `+${e.pontos}`;
            li.append(em);
        }
        return li;
    }));

    atualizarRelogio(anterior);
    anunciar(anterior);
}

function anunciar(anterior) {
    if (!anterior) return;
    if (visao.rodada > anterior.rodada) mensagem(`Rodada ${visao.rodada}: novas pedras distribuídas.`, "info", 4000);
    const ganhoEu = (visao.eu?.pontuacao ?? 0) - (anterior.eu?.pontuacao ?? 0);
    const ganhoOp = (visao.oponente?.pontuacao ?? 0) - (anterior.oponente?.pontuacao ?? 0);
    if (ganhoEu > 0) mensagem(`Quina! +${ganhoEu} ponto${ganhoEu > 1 ? "s" : ""} para você`, "ponto");
    else if (ganhoOp > 0) mensagem(`${visao.oponente?.nome ?? "Oponente"} marcou +${ganhoOp}`, "info");
}

function atualizarRelogio(anterior) {
    if (visao.minhaVez && (!anterior || !anterior.minhaVez || anterior.versao !== visao.versao && inicioVez === null)) {
        inicioVez = performance.now();
    }
    if (!visao.minhaVez) inicioVez = null;
}

setInterval(() => {
    const relogio = $("relogio");
    const circulo = relogio.querySelector(".progresso");
    if (inicioVez === null) {
        campo("segundos").textContent = "–";
        circulo.style.strokeDashoffset = 176;
        relogio.classList.remove("alerta", "critico");
        return;
    }
    const s = Math.floor((performance.now() - inicioVez) / 1000);
    campo("segundos").textContent = s;
    circulo.style.strokeDashoffset = 176 * (1 - Math.min(s, 60) / 60);
    relogio.classList.toggle("alerta", s > 10 && s <= 30);
    relogio.classList.toggle("critico", s > 30);
}, 250);

/** Applies a confirmed state, ignoring anything older than what is on screen. */
function aplicar(nova) {
    if (!nova || nova.versao < visao.versao) return;
    const anterior = visao;
    visao = nova;
    selecionada = null;
    $("escolha-lado").hidden = true;
    render(anterior);
}

async function requisitar(url, corpo) {
    const resposta = await fetch(url, {
        method: corpo === undefined ? "GET" : "POST",
        headers: corpo === undefined ? {} : { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: corpo === undefined ? undefined : JSON.stringify(corpo),
        credentials: "same-origin",
        cache: "no-store"
    });
    if (resposta.status === 401) { location.href = "/Entrar"; throw new Error("sessão expirada"); }
    if (resposta.status === 412) { location.href = "/Perfis"; throw new Error("sem perfil"); }
    return resposta;
}

/**
 * Sends one intention. The same idempotency key is reused on network retries so the
 * API never applies it twice; the screen only changes with the confirmed state.
 */
async function enviar(tipo, extra = {}) {
    if (enviando) return;
    enviando = true;
    render(null);
    const corpo = { tipo, versao: visao.versao, chaveIdempotencia: uuid(), ...extra };
    try {
        for (let tentativa = 1; ; tentativa++) {
            try {
                const r = await requisitar(`/bff/partidas/${partidaId}/acoes`, corpo);
                const dados = await r.json();
                if (r.ok) {
                    enviando = false;
                    aplicar(dados);
                } else {
                    enviando = false;
                    mensagem(dados.mensagem ?? "Ação rejeitada. A partida não avançou.", "erro", 5000);
                    if (dados.visao) aplicar(dados.visao);
                    else render(null);
                }
                return;
            } catch (erro) {
                if (tentativa >= 3) throw erro;
                await new Promise((ok) => setTimeout(ok, 500 * tentativa));
            }
        }
    } catch {
        enviando = false;
        render(null);
        mensagem("Sem conexão com o servidor. Sua ação não foi confirmada.", "erro", 5000);
    }
}

function aoClicarCena(alvo) {
    if (!controlesAtivos()) return;
    if (alvo.tipo === "lado" && selecionada) {
        jogar(selecionada, alvo.lado);
        return;
    }
    if (alvo.tipo !== "pedra") return;

    const opcoes = visao.jogadas.filter((j) => chave(j.valorA, j.valorB) === alvo.chave);
    if (opcoes.length === 0) {
        mensagem("Essa pedra não encaixa em nenhuma ponta.", "erro");
        return;
    }
    const pontasIguais = visao.pontas && visao.pontas.esquerda === visao.pontas.direita;
    if (opcoes.length === 1 || pontasIguais) {
        jogar(opcoes[0], opcoes[0].lado);
        return;
    }
    selecionada = opcoes[0];
    const barra = $("escolha-lado");
    for (const botao of barra.querySelectorAll("button[data-lado]")) {
        const lado = botao.dataset.lado;
        if (lado === "cancelar") continue;
        const op = opcoes.find((o) => String(o.lado) === lado);
        botao.hidden = !op;
        botao.querySelector("em").textContent = op?.pontos ? `(+${op.pontos})` : "";
    }
    barra.hidden = false;
    cena.mostrarFantasmas(opcoes.map((o) => o.lado));
}

function jogar(jogada, lado) {
    $("escolha-lado").hidden = true;
    cena.esconderFantasmas();
    enviar("jogar", { valorA: jogada.valorA, valorB: jogada.valorB, lado: Number(lado) });
}

$("escolha-lado").addEventListener("click", (e) => {
    const botao = e.target.closest("button[data-lado]");
    if (!botao) return;
    if (botao.dataset.lado === "cancelar" || !selecionada) {
        selecionada = null;
        $("escolha-lado").hidden = true;
        cena.esconderFantasmas();
        return;
    }
    jogar(selecionada, botao.dataset.lado);
});
$("btn-comprar").addEventListener("click", () => enviar("comprar"));
$("btn-passar").addEventListener("click", () => enviar("passar"));
$("btn-iniciar").addEventListener("click", () => enviar("iniciar"));

$("autonomo").addEventListener("change", async (e) => {
    const ativo = e.target.checked;
    try {
        const r = await requisitar(`/bff/partidas/${partidaId}/autonomo`, { ativo });
        if (!r.ok) throw new Error();
        const dados = await r.json();
        visao = { ...visao, autonomo: dados.ativo };
        render(null);
        mensagem(dados.ativo ? "Jogador autônomo assumiu a partida." : "Você voltou ao controle.", "info");
    } catch {
        e.target.checked = !ativo;
        mensagem("Não foi possível alterar o jogador autônomo.", "erro");
    }
});

async function consultar() {
    if (!enviando) {
        try {
            const r = await requisitar(`/bff/partidas/${partidaId}/estado?versao=${visao.versao}`);
            if (r.status === 200) aplicar(await r.json());
            else if (r.status !== 204) {
                const dados = await r.json().catch(() => ({}));
                mensagem(dados.mensagem ?? "Falha ao atualizar a mesa.", "erro");
            }
        } catch {
            mensagem("Reconectando ao servidor…", "erro", 2000);
        }
    }
    if (visao.situacao !== Situacao.Encerrada || visao.autonomo)
        setTimeout(consultar, document.hidden ? INTERVALO_OCULTO : INTERVALO_CONSULTA);
}

render(null);
setTimeout(consultar, INTERVALO_CONSULTA);
