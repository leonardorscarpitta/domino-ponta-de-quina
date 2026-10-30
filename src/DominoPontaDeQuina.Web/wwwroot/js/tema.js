const COOKIE = "domino.tema";
const seletor = document.getElementById("tema");

seletor?.addEventListener("change", () => {
    const tema = seletor.value;
    document.documentElement.dataset.tema = tema;
    const seguro = location.protocol === "https:" ? "; Secure" : "";
    document.cookie = `${COOKIE}=${encodeURIComponent(tema)}; Path=/; Max-Age=31536000; SameSite=Strict${seguro}`;
    window.dispatchEvent(new CustomEvent("tema-alterado", { detail: tema }));
});
