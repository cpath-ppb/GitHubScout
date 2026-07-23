const REFRESH_MS = 5000;

const itemsBody = document.querySelector("#items-table tbody");
const activityFeed = document.querySelector("#activity-feed");
const lastRefreshed = document.querySelector("#last-refreshed");

const dialog = document.querySelector("#add-item-dialog");
const form = document.querySelector("#add-item-form");
const kindSelect = document.querySelector("#kind-select");
const prField = document.querySelector("#pr-field");
const branchField = document.querySelector("#branch-field");

document.querySelector("#add-item-btn").addEventListener("click", () => dialog.showModal());
document.querySelector("#cancel-add").addEventListener("click", () => dialog.close());

kindSelect.addEventListener("change", () => {
  const isPr = kindSelect.value === "PullRequest";
  prField.style.display = isPr ? "block" : "none";
  branchField.style.display = isPr ? "none" : "block";
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(form).entries());

  let response;
  if (data.url && data.url.trim()) {
    response = await fetch("/api/watchlist/from-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url: data.url.trim(), alias: data.alias || null }),
    });
  } else {
    const payload = {
      owner: data.owner,
      repo: data.repo,
      kind: data.kind,
      alias: data.alias || null,
      pullRequestNumber: data.kind === "PullRequest" ? Number(data.pullRequestNumber) : null,
      branchName: data.kind === "Branch" ? data.branchName : null,
    };
    response = await fetch("/api/watchlist", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
  }

  if (response.ok) {
    dialog.close();
    form.reset();
    await refresh();
  } else {
    alert(await response.text());
  }
});

function badge(text, kind) {
  return `<span class="badge ${kind}">${text}</span>`;
}

function checksBadge(state) {
  switch (state) {
    case "Success": return badge("passing", "success");
    case "Failure": return badge("failing", "failure");
    case "Pending": return badge("pending", "pending");
    case "NoChecks": return badge("none", "muted");
    default: return badge("unknown", "muted");
  }
}

function reviewBadge(state, pendingReviewers) {
  let html;
  switch (state) {
    case "Approved": html = badge("approved", "success"); break;
    case "ChangesRequested": html = badge("changes requested", "failure"); break;
    case "ReviewRequired": html = badge("awaiting review", "pending"); break;
    case "NoReviewRequired": html = badge("n/a", "muted"); break;
    default: html = badge("unknown", "muted");
  }
  if (state === "ReviewRequired" && pendingReviewers && pendingReviewers.length > 0) {
    html += ` <span class="pending-reviewers">from ${pendingReviewers.join(", ")}</span>`;
  }
  return html;
}

async function refresh() {
  try {
    const [statusResponse, activityResponse] = await Promise.all([
      fetch("/api/status"),
      fetch("/api/activity?count=30"),
    ]);

    const items = await statusResponse.json();
    const activity = await activityResponse.json();

    renderItems(items);
    renderActivity(activity);

    lastRefreshed.textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch (err) {
    lastRefreshed.textContent = "Failed to reach GithubScout API";
  }
}

function renderItems(items) {
  itemsBody.innerHTML = "";
  for (const entry of items) {
    const status = entry.status;
    const row = document.createElement("tr");
    row.innerHTML = `
      <td><a href="${status?.htmlUrl ?? "#"}" target="_blank" rel="noopener">${entry.displayName}</a></td>
      <td>${status?.state ?? "pending"}</td>
      <td>${checksBadge(status?.checksState)}</td>
      <td>${reviewBadge(status?.reviewState, status?.pendingReviewers)}</td>
      <td>${status?.latestCommitSha ? status.latestCommitSha.substring(0, 7) : "-"}</td>
      <td>${status?.checkedAtUtc ? new Date(status.checkedAtUtc).toLocaleTimeString() : "-"}</td>
      <td><button class="secondary" data-id="${entry.id}">Remove</button></td>
    `;
    row.querySelector("button").addEventListener("click", async (e) => {
      await fetch(`/api/watchlist/${e.target.dataset.id}`, { method: "DELETE" });
      await refresh();
    });
    itemsBody.appendChild(row);
  }
}

function renderActivity(activity) {
  activityFeed.innerHTML = "";
  for (const item of activity) {
    const li = document.createElement("li");
    const time = new Date(item.timestampUtc).toLocaleTimeString();
    li.innerHTML = `<time>${time}</time><strong>${item.watchItemDisplayName}</strong> — ${item.message}`;
    activityFeed.appendChild(li);
  }
}

refresh();
setInterval(refresh, REFRESH_MS);
