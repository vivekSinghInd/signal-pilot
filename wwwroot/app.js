const state = { result: null, selected: null };
const $ = (id) => document.getElementById(id);

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>'"]/g, (char) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
  }[char]));
}

function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString(undefined, { day: "2-digit", month: "short", year: "numeric" });
}

function formatTime(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

function setLoading(loading) {
  $("loadingState").hidden = !loading;
  if (loading) {
    $("tableWrap").hidden = true;
    $("emptyState").hidden = true;
  }
}

function statusClass(status) {
  return `status-${String(status || "watchlist").toLowerCase()}`;
}

function renderMetrics(result) {
  $("signalDate").textContent = formatDate(result.latestSignalDate);
  $("candidateCount").textContent = result.candidates?.length ?? 0;
  $("aiStatus").textContent = result.aiEnabled ? "Enabled" : "Fallback";
  $("executionStatus").textContent = result.liveOrdersEnabled ? "Enabled" : "Disabled";
  $("paperCap").textContent = result.paperTradingEnabled ? `₹${Number(result.maxPaperCapitalPerTrade || 0).toLocaleString("en-IN")}` : "Off";
  $("lastUpdated").textContent = `Updated ${formatTime(result.generatedAt)}`;
}

function renderRows(candidates) {
  const body = $("candidateRows");
  body.innerHTML = candidates.map((item, index) => {
    const decision = item.decision || {};
    const signals = item.screenerCount ? `${item.screenerCount} screeners` : "—";
    const rsi = [item.maxRsi, item.maxWeeklyRsi, item.maxMonthlyRsi].map((value) => value ? Number(value).toFixed(0) : "—").join(" / ");
    const newsCount = item.news?.length ?? 0;
    return `<tr>
      <td class="rank">${String(index + 1).padStart(2, "0")}</td>
      <td><button class="symbol-button" data-symbol="${escapeHtml(item.symbol)}" type="button">${escapeHtml(item.symbol)}</button></td>
      <td class="score">${Number(item.preliminaryScore || 0).toFixed(0)}</td>
      <td><span class="status-pill ${statusClass(decision.status)}">${escapeHtml((decision.status || "WATCHLIST").replaceAll("_", " "))}</span></td>
      <td class="muted-cell">${escapeHtml(item.marketCapCategory || "Unclassified")}</td>
      <td>${escapeHtml(signals)}</td>
      <td class="muted-cell">${rsi}</td>
      <td class="muted-cell">${newsCount ? `${newsCount} article${newsCount === 1 ? "" : "s"}` : "None found"}</td>
    </tr>`;
  }).join("");
  body.querySelectorAll("[data-symbol]").forEach((button) => button.addEventListener("click", () => selectCandidate(button.dataset.symbol)));
}

function renderDetail(item) {
  const decision = item.decision || {};
  $("detailTitle").textContent = item.symbol;
  $("detailStatus").className = `status-pill ${statusClass(decision.status)}`;
  $("detailStatus").textContent = (decision.status || "WATCHLIST").replaceAll("_", " ");
  $("detailScore").textContent = Number(item.preliminaryScore || 0).toFixed(0);
  $("detailThesis").textContent = decision.thesisSummary || "No thesis summary is available.";
  $("detailUpside").textContent = decision.upsideCase || "No upside case recorded.";
  $("detailDownside").textContent = decision.downsideCase || "No downside case recorded.";
  $("detailCatalyst").textContent = decision.catalyst || "No catalyst recorded.";
  renderList($("detailRisks"), decision.riskFlags, "No risk flags recorded");
  renderList($("detailEvidence"), decision.evidence?.length ? decision.evidence : item.evidence, "No scanner evidence recorded");
  const articles = item.news || [];
  $("detailNews").innerHTML = articles.length ? articles.map((article) => `<article class="news-item">
    <a href="${escapeHtml(article.url || "#")}" target="_blank" rel="noreferrer">${escapeHtml(article.title || "Untitled article")}</a>
    <div class="news-meta">${escapeHtml(article.source || "TradingView")} · ${formatDate(article.publishedAt)}</div>
  </article>`).join("") : `<div class="news-item"><div class="news-meta">No recent news was found in the configured window.</div></div>`;
  $("detailPanel").hidden = false;
  $("detailPanel").scrollIntoView({ behavior: "smooth", block: "start" });
}

function renderList(element, items, emptyText) {
  const values = Array.isArray(items) && items.length ? items : [emptyText];
  element.innerHTML = values.map((item) => `<li>${escapeHtml(item)}</li>`).join("");
}

function selectCandidate(symbol) {
  const item = state.result?.candidates?.find((candidate) => candidate.symbol === symbol);
  if (!item) return;
  state.selected = symbol;
  renderDetail(item);
}

async function loadScan() {
  setLoading(true);
  $("errorBanner").hidden = true;
  try {
    const limit = $("limitSelect").value;
    const response = await fetch(`/api/opportunities?limit=${encodeURIComponent(limit)}`, { cache: "no-store" });
    if (!response.ok) throw new Error(`Scan request failed (${response.status})`);
    state.result = await response.json();
    renderMetrics(state.result);
    const candidates = state.result.candidates || [];
    $("emptyState").hidden = candidates.length > 0;
    $("tableWrap").hidden = candidates.length === 0;
    if (candidates.length) renderRows(candidates);
    if (state.selected) {
      const selected = candidates.find((candidate) => candidate.symbol === state.selected);
      if (selected) renderDetail(selected);
    }
  } catch (error) {
    $("errorBanner").textContent = `${error.message}. Confirm the service is running on port 5010.`;
    $("errorBanner").hidden = false;
    $("emptyState").hidden = false;
    $("tableWrap").hidden = true;
  } finally {
    setLoading(false);
  }
}

$("refreshButton").addEventListener("click", loadScan);
$("limitSelect").addEventListener("change", loadScan);
$("closeDetail").addEventListener("click", () => { $("detailPanel").hidden = true; state.selected = null; });
loadScan();
setInterval(loadScan, 15 * 60 * 1000);
