#!/usr/bin/env node

const crypto = require("crypto");
const fs = require("fs");
const fsp = require("fs/promises");
const http = require("http");
const os = require("os");
const path = require("path");
const { spawn, spawnSync } = require("child_process");

const DEFAULT_PORT = 46330;
const SUPPORTED_PLATFORMS = new Set(["Windows", "Mac", "iOS", "Android", "Linux"]);
const RSYNC_EXCLUDES = [
  ".git/",
  "Library/",
  "Temp/",
  "Logs/",
  "Obj/",
  "UserSettings/",
  "MemoryCaptures/",
  "Build/",
  "Builds/",
  "bundles/",
  ".DS_Store",
  "*.csproj",
  "*.sln",
];

let buildQueue = Promise.resolve();
let rsyncChecked = false;
let hasRsync = false;
const buildJobs = new Map();
const JOB_TTL_MS = 12 * 60 * 60 * 1000;
const JOB_CANCELED_ERROR = "Build job canceled.";

function log(msg) {
  const now = new Date().toISOString();
  process.stdout.write(`[${now}] ${msg}\n`);
}

function parseArgs(argv) {
  const args = { command: "server", port: DEFAULT_PORT };
  const extra = argv.slice(2);
  if (extra.length > 0 && !extra[0].startsWith("-")) {
    args.command = extra[0];
  }

  for (let i = 0; i < extra.length; i += 1) {
    const current = extra[i];
    if (current === "--port") {
      const value = Number(extra[i + 1]);
      if (Number.isFinite(value) && value > 0) {
        args.port = value;
      }
    }
  }

  return args;
}

function summarizeError(message, max = 180) {
  if (!message) {
    return "";
  }

  const singleLine = String(message).replace(/\s+/g, " ").trim();
  if (singleLine.length <= max) {
    return singleLine;
  }
  return `${singleLine.slice(0, max - 3)}...`;
}

function formatDurationShort(seconds) {
  const totalSeconds = Math.max(0, Math.floor(Number(seconds) || 0));
  const minutes = Math.floor(totalSeconds / 60);
  const remainingSeconds = totalSeconds % 60;
  if (minutes <= 0) {
    return `${remainingSeconds}s`;
  }
  return `${minutes}m ${remainingSeconds}s`;
}

function isCancellationError(error) {
  if (!error) {
    return false;
  }

  const message = String(error.message || error);
  return message.includes(JOB_CANCELED_ERROR) || message.toLowerCase().includes("abort");
}

function enqueue(task) {
  const pending = buildQueue.then(task, task);
  buildQueue = pending.catch(() => {});
  return pending;
}

function json(res, statusCode, payload) {
  const body = Buffer.from(JSON.stringify(payload), "utf8");
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Cache-Control": "no-store",
    Connection: "close",
    "Content-Length": String(body.length),
  });
  res.end(body);
}

async function readJsonBody(req) {
  const chunks = [];
  for await (const chunk of req) {
    chunks.push(chunk);
  }

  const raw = Buffer.concat(chunks).toString("utf8");
  if (!raw) {
    return {};
  }

  return JSON.parse(raw);
}

function ensureRsyncAvailable() {
  if (rsyncChecked) {
    return hasRsync;
  }

  rsyncChecked = true;
  const result = spawnSync("rsync", ["--version"], { stdio: "ignore" });
  hasRsync = result.status === 0;
  return hasRsync;
}

function runCommand(executable, args, options = {}) {
  return new Promise((resolve, reject) => {
    if (options.signal && options.signal.aborted) {
      reject(new Error(JOB_CANCELED_ERROR));
      return;
    }

    const child = spawn(executable, args, {
      cwd: options.cwd,
      stdio: ["ignore", "pipe", "pipe"],
      env: process.env,
    });
    if (typeof options.onSpawn === "function") {
      options.onSpawn(child);
    }

    let stdout = "";
    let stderr = "";
    let stdoutLineBuffer = "";
    let stderrLineBuffer = "";
    let settled = false;
    let killTimer = null;

    const flushLineBuffer = (buffer, isStdErr) => {
      if (!buffer) {
        return "";
      }
      const callback = isStdErr ? options.onStderrLine : options.onStdoutLine;
      if (typeof callback === "function") {
        callback(buffer);
      }
      return "";
    };

    const pushChunkLines = (chunkText, existingBuffer, isStdErr) => {
      const callback = isStdErr ? options.onStderrLine : options.onStdoutLine;
      if (typeof callback !== "function") {
        return "";
      }
      const combined = existingBuffer + chunkText;
      const parts = combined.split(/\r?\n/);
      for (let i = 0; i < parts.length - 1; i += 1) {
        if (parts[i]) {
          callback(parts[i]);
        }
      }
      return parts[parts.length - 1] || "";
    };

    const cleanupAbort = () => {
      if (options.signal && typeof options.signal.removeEventListener === "function") {
        options.signal.removeEventListener("abort", onAbort);
      }
      if (killTimer) {
        clearTimeout(killTimer);
        killTimer = null;
      }
    };

    const onAbort = () => {
      if (settled) {
        return;
      }
      child.kill("SIGTERM");
      killTimer = setTimeout(() => {
        if (!settled) {
          child.kill("SIGKILL");
        }
      }, 5000);
    };

    if (options.signal && typeof options.signal.addEventListener === "function") {
      options.signal.addEventListener("abort", onAbort);
      if (options.signal.aborted) {
        onAbort();
      }
    }

    child.stdout.on("data", (chunk) => {
      const text = chunk.toString();
      stdout += text;
      stdoutLineBuffer = pushChunkLines(text, stdoutLineBuffer, false);
    });

    child.stderr.on("data", (chunk) => {
      const text = chunk.toString();
      stderr += text;
      stderrLineBuffer = pushChunkLines(text, stderrLineBuffer, true);
    });

    child.on("error", (error) => {
      settled = true;
      cleanupAbort();
      reject(error);
    });

    child.on("close", (code) => {
      settled = true;
      cleanupAbort();
      stdoutLineBuffer = flushLineBuffer(stdoutLineBuffer, false);
      stderrLineBuffer = flushLineBuffer(stderrLineBuffer, true);
      if (options.signal && options.signal.aborted) {
        reject(new Error(JOB_CANCELED_ERROR));
        return;
      }
      if (code === 0) {
        resolve({ stdout, stderr });
        return;
      }

      const cmdText = [executable].concat(args).join(" ");
      reject(new Error(`Command failed (${code}): ${cmdText}\n${stderr || stdout}`));
    });
  });
}

function trailingSlash(value) {
  if (value.endsWith(path.sep)) {
    return value;
  }

  return value + path.sep;
}

async function syncSourceToShadow(sourceProjectPath, shadowProjectPath, signal) {
  if (!ensureRsyncAvailable()) {
    throw new Error("rsync is required for local shadow build sync but was not found.");
  }

  await fsp.mkdir(shadowProjectPath, { recursive: true });
  const args = ["-a", "--delete"];
  for (const exclude of RSYNC_EXCLUDES) {
    args.push("--exclude", exclude);
  }

  args.push(trailingSlash(sourceProjectPath));
  args.push(trailingSlash(shadowProjectPath));

  await runCommand("rsync", args, { cwd: sourceProjectPath, signal });
}

async function pathExists(targetPath) {
  try {
    await fsp.lstat(targetPath);
    return true;
  } catch {
    return false;
  }
}

async function tryReadLogTailLine(filePath) {
  try {
    const stat = await fsp.stat(filePath);
    if (!stat || stat.size <= 0) {
      return "";
    }

    const bytesToRead = Math.min(stat.size, 8192);
    const start = stat.size - bytesToRead;
    const fd = await fsp.open(filePath, "r");
    try {
      const buffer = Buffer.alloc(bytesToRead);
      await fd.read(buffer, 0, bytesToRead, start);
      const text = buffer.toString("utf8");
      const lines = text
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean);
      if (lines.length === 0) {
        return "";
      }

      return summarizeError(lines[lines.length - 1], 180);
    } finally {
      await fd.close();
    }
  } catch {
    return "";
  }
}

function parseFileDependencyPath(spec) {
  if (typeof spec !== "string" || !spec.startsWith("file:")) {
    return null;
  }

  const raw = spec.slice("file:".length).trim();
  if (!raw) {
    return null;
  }

  return decodeURIComponent(raw);
}

async function linkLocalFileDependencies(sourceProjectPath, shadowProjectPath, shadowRoot) {
  const manifestPath = path.join(sourceProjectPath, "Packages", "manifest.json");
  if (!(await pathExists(manifestPath))) {
    return;
  }

  let manifest;
  try {
    manifest = JSON.parse(await fsp.readFile(manifestPath, "utf8"));
  } catch (error) {
    throw new Error(`Failed to parse manifest.json for local package linking: ${error.message}`);
  }

  const sourceManifestDir = path.dirname(manifestPath);
  const shadowManifestDir = path.join(shadowProjectPath, "Packages");
  const dependencies = manifest && manifest.dependencies ? manifest.dependencies : {};
  const entries = Object.entries(dependencies);
  for (const [, spec] of entries) {
    const relDependencyPath = parseFileDependencyPath(spec);
    if (!relDependencyPath) {
      continue;
    }

    const sourceDependencyPath = path.resolve(sourceManifestDir, relDependencyPath);
    if (!(await pathExists(sourceDependencyPath))) {
      throw new Error(`Local file dependency not found: ${sourceDependencyPath} (${spec})`);
    }

    const shadowDependencyPath = path.resolve(shadowManifestDir, relDependencyPath);
    if (!shadowDependencyPath.startsWith(path.resolve(shadowRoot) + path.sep)) {
      throw new Error(`Refusing to materialize dependency outside shadow root: ${shadowDependencyPath}`);
    }

    await fsp.mkdir(path.dirname(shadowDependencyPath), { recursive: true });
    if (await pathExists(shadowDependencyPath)) {
      try {
        const stat = await fsp.lstat(shadowDependencyPath);
        if (stat.isSymbolicLink()) {
          const existingTarget = await fsp.readlink(shadowDependencyPath);
          const resolvedTarget = path.resolve(path.dirname(shadowDependencyPath), existingTarget);
          if (resolvedTarget === sourceDependencyPath) {
            continue;
          }
        }

        await fsp.rm(shadowDependencyPath, { recursive: true, force: true });
      } catch (error) {
        if (!["ENOENT"].includes(error.code)) {
          throw error;
        }
      }
    }

    const linkType = process.platform === "win32" ? "junction" : "dir";
    try {
      await fsp.symlink(sourceDependencyPath, shadowDependencyPath, linkType);
    } catch (error) {
      if (error.code !== "EEXIST") {
        throw error;
      }
    }
  }
}

function getDefaultShadowRoot(projectPath) {
  const hash = crypto.createHash("sha1").update(projectPath).digest("hex").slice(0, 12);
  if (process.platform === "darwin") {
    return path.join(os.homedir(), "Library", "Application Support", "AirshipShadowBuilds", hash);
  }

  if (process.platform === "win32") {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
    return path.join(localAppData, "AirshipShadowBuilds", hash);
  }

  return path.join(os.homedir(), ".airship-shadow-builds", hash);
}

function sanitizeName(value) {
  return value.replace(/[^a-z0-9_.-]/gi, "_");
}

function resolveUnityExecutable(unityPath) {
  if (!unityPath) {
    return unityPath;
  }

  if (process.platform === "darwin" && unityPath.endsWith(".app")) {
    return path.join(unityPath, "Contents", "MacOS", "Unity");
  }

  return unityPath;
}

function validateRequest(input) {
  if (!input || typeof input !== "object") {
    throw new Error("Body must be a JSON object.");
  }

  const projectPath = path.resolve(String(input.projectPath || ""));
  const unityPath = resolveUnityExecutable(path.resolve(String(input.unityPath || "")));
  const gameId = String(input.gameId || "").trim();
  const useCache = input.useCache !== false;
  const shadowRoot = input.shadowRoot
    ? path.resolve(String(input.shadowRoot))
    : getDefaultShadowRoot(projectPath);

  if (!projectPath || !fs.existsSync(projectPath)) {
    throw new Error(`Invalid projectPath: ${input.projectPath}`);
  }

  if (!unityPath || !fs.existsSync(unityPath)) {
    throw new Error(`Invalid unityPath: ${input.unityPath}`);
  }

  if (!gameId) {
    throw new Error("gameId is required.");
  }

  const rawPlatforms = Array.isArray(input.platforms) ? input.platforms : [];
  if (rawPlatforms.length === 0) {
    throw new Error("platforms must contain at least one platform.");
  }

  const platforms = rawPlatforms.map((p) => String(p));
  for (const platform of platforms) {
    if (!SUPPORTED_PLATFORMS.has(platform)) {
      throw new Error(`Unsupported platform '${platform}'.`);
    }
  }

  let maxParallel = Number(input.maxParallel || 1);
  if (!Number.isFinite(maxParallel) || maxParallel < 1) {
    maxParallel = 1;
  }

  maxParallel = Math.floor(maxParallel);

  return {
    projectPath,
    unityPath,
    gameId,
    platforms,
    useCache,
    shadowRoot,
    maxParallel,
  };
}

async function runUnityBuild({
  unityPath,
  shadowProjectPath,
  platform,
  useCache,
  shadowRoot,
  signal,
  onLogFile,
  onHeartbeat,
}) {
  const logsDir = path.join(shadowRoot, "logs");
  await fsp.mkdir(logsDir, { recursive: true });
  const logFile = path.join(logsDir, `${Date.now()}_${platform}.log`);
  const buildStartedAt = Date.now();
  if (typeof onLogFile === "function") {
    onLogFile(logFile);
  }

  const args = [
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath",
    shadowProjectPath,
    "-executeMethod",
    "AirshipBuildAgentEntry.BuildGameAssetBundlesFromCommandLine",
    "-airshipBuildPlatform",
    platform,
    "-airshipBuildUseCache",
    useCache ? "true" : "false",
    "-logFile",
    logFile,
  ];

  let heartbeatBusy = false;
  const heartbeat = async () => {
    if (heartbeatBusy) {
      return;
    }

    heartbeatBusy = true;
    try {
      if (typeof onHeartbeat === "function") {
        const elapsedSeconds = (Date.now() - buildStartedAt) / 1000;
        const logTail = await tryReadLogTailLine(logFile);
        onHeartbeat({
          elapsedSeconds,
          logTail,
        });
      }
    } finally {
      heartbeatBusy = false;
    }
  };

  const heartbeatTimer = setInterval(() => {
    void heartbeat();
  }, 5000);

  try {
    await heartbeat();
    await runCommand(unityPath, args, { cwd: shadowProjectPath, signal });
    return logFile;
  } catch (error) {
    if (error && typeof error === "object") {
      error.logFile = logFile;
    }
    throw error;
  } finally {
    clearInterval(heartbeatTimer);
  }
}

async function copyPlatformOutput({ projectPath, shadowProjectPath, gameId, platform, signal }) {
  const relativeOutput = path.join("bundles", "games", `${gameId}_vLocalBuild`, platform);
  const shadowOutput = path.join(shadowProjectPath, relativeOutput);
  const sourceOutput = path.join(projectPath, relativeOutput);

  if (!fs.existsSync(shadowOutput)) {
    throw new Error(`Expected build output not found: ${shadowOutput}`);
  }

  if (!ensureRsyncAvailable()) {
    throw new Error("rsync is required for copying build output but was not found.");
  }

  await fsp.mkdir(sourceOutput, { recursive: true });
  const args = ["-a", "--delete", trailingSlash(shadowOutput), trailingSlash(sourceOutput)];
  await runCommand("rsync", args, { cwd: projectPath, signal });

  return sourceOutput;
}

async function runWithConcurrency(items, limit, worker, shouldStop) {
  const results = new Array(items.length);
  let index = 0;

  async function runOne() {
    while (true) {
      if (typeof shouldStop === "function" && shouldStop()) {
        return;
      }

      const current = index;
      index += 1;
      if (current >= items.length) {
        return;
      }

      results[current] = await worker(items[current], current);
    }
  }

  const workers = [];
  const workerCount = Math.min(limit, items.length);
  for (let i = 0; i < workerCount; i += 1) {
    workers.push(runOne());
  }

  await Promise.all(workers);
  return results;
}

function createJobId() {
  return `${Date.now().toString(36)}-${crypto.randomBytes(4).toString("hex")}`;
}

function cleanupOldJobs() {
  const now = Date.now();
  for (const [id, job] of buildJobs.entries()) {
    if (!job.doneAt) {
      continue;
    }

    if (now - job.doneAt > JOB_TTL_MS) {
      buildJobs.delete(id);
    }
  }
}

function getPlatformState(job, platform) {
  const now = Date.now();
  if (!job.platformStates[platform]) {
    job.platformStates[platform] = {
      platform,
      status: "pending",
      progress: 0,
      message: "Queued",
      detail: "",
      success: null,
      durationSeconds: 0,
      error: "",
      logFile: "",
      shadowProjectPath: "",
      updatedAtUnixMs: now,
      statusSinceUnixMs: now,
      staleSeconds: 0,
      stalled: false,
      elapsedStatusSeconds: 0,
    };
  }

  return job.platformStates[platform];
}

function updatePlatformState(job, platform, patch) {
  const state = getPlatformState(job, platform);
  const now = Date.now();
  if (patch && Object.prototype.hasOwnProperty.call(patch, "status") && patch.status && patch.status !== state.status) {
    state.statusSinceUnixMs = now;
  }
  Object.assign(state, patch);
  state.updatedAtUnixMs = now;
  job.updatedAt = now;
}

function computeJobStatus(job) {
  const now = Date.now();
  const states = job.platformOrder.map((platform) => getPlatformState(job, platform));
  const projectedStates = states.map((state) => {
    const updatedAtUnixMs = Number.isFinite(state.updatedAtUnixMs) ? state.updatedAtUnixMs : now;
    const statusSinceUnixMs = Number.isFinite(state.statusSinceUnixMs) ? state.statusSinceUnixMs : updatedAtUnixMs;
    const activeStatus = state.status === "syncing"
      || state.status === "linking"
      || state.status === "building"
      || state.status === "copying";
    const staleSeconds = Math.max(0, Math.floor((now - updatedAtUnixMs) / 1000));
    return {
      ...state,
      updatedAtUnixMs,
      statusSinceUnixMs,
      staleSeconds: activeStatus ? staleSeconds : 0,
      stalled: activeStatus && staleSeconds >= 90,
      elapsedStatusSeconds: Math.max(0, Math.floor((now - statusSinceUnixMs) / 1000)),
    };
  });
  const totalPlatforms = projectedStates.length;
  const completedPlatforms = projectedStates.filter((state) => state.status === "done" || state.status === "failed").length;
  const totalProgress = totalPlatforms === 0
    ? 1
    : projectedStates.reduce((acc, state) => acc + (Number.isFinite(state.progress) ? state.progress : 0), 0) / totalPlatforms;

  return {
    ok: job.done ? job.ok : true,
    done: job.done,
    jobId: job.jobId,
    state: job.state,
    message: job.message,
    totalProgress: Math.max(0, Math.min(1, totalProgress)),
    totalPlatforms,
    completedPlatforms,
    totalDurationSeconds: job.done ? job.totalDurationSeconds : (now - job.startedAt) / 1000,
    nowUnixMs: now,
    updatedAtUnixMs: job.updatedAt,
    shadowRoot: job.shadowRoot,
    platforms: projectedStates,
    results: job.results || [],
  };
}

function formatFailedPlatforms(results) {
  return results
    .filter((result) => !result.success)
    .map((result) => result.platform)
    .join(", ");
}

function requestJobCancel(job) {
  if (!job || job.done || job.cancelRequested) {
    return false;
  }

  job.cancelRequested = true;
  job.state = "canceling";
  job.message = "Cancel requested.";
  job.updatedAt = Date.now();
  if (job.cancelController && typeof job.cancelController.abort === "function") {
    job.cancelController.abort();
  }

  for (const platform of job.platformOrder) {
    const state = getPlatformState(job, platform);
    if (state.status === "pending") {
      updatePlatformState(job, platform, {
        status: "failed",
        progress: 1,
        message: "Canceled",
        detail: "Build canceled before this platform started",
        success: false,
        error: JOB_CANCELED_ERROR,
      });
    } else if (state.status !== "done" && state.status !== "failed") {
      updatePlatformState(job, platform, {
        message: "Cancel requested",
      });
    }
  }

  return true;
}

function createBuildJob(payload) {
  cleanupOldJobs();
  const request = validateRequest(payload);
  const jobId = createJobId();
  const job = {
    jobId,
    state: "queued",
    message: "Queued",
    createdAt: Date.now(),
    startedAt: Date.now(),
    updatedAt: Date.now(),
    doneAt: 0,
    done: false,
    ok: false,
    totalDurationSeconds: 0,
    shadowRoot: request.shadowRoot,
    platformOrder: request.platforms.slice(),
    platformStates: {},
    results: [],
    cancelRequested: false,
    cancelController: new AbortController(),
  };

  for (const platform of request.platforms) {
    getPlatformState(job, platform);
  }

  buildJobs.set(jobId, job);
  enqueue(async () => {
    await runBuildJob(job, request);
  });

  return job;
}

async function runBuildJob(job, request) {
  const startedAt = Date.now();
  job.state = "running";
  job.startedAt = startedAt;
  job.updatedAt = startedAt;
  job.message = "Building";

  try {
    if (job.cancelRequested) {
      throw new Error(JOB_CANCELED_ERROR);
    }

    await fsp.mkdir(request.shadowRoot, { recursive: true });

    const rawResults = await runWithConcurrency(
      request.platforms,
      request.maxParallel,
      async (platform) => buildSinglePlatform(request, platform, (update) => {
        updatePlatformState(job, platform, update);
      }, job.cancelController.signal),
      () => job.cancelRequested,
    );
    const results = request.platforms.map((platform, index) => {
      if (rawResults[index]) {
        return rawResults[index];
      }

      updatePlatformState(job, platform, {
        status: "failed",
        progress: 1,
        message: "Canceled",
        detail: "",
        success: false,
        error: JOB_CANCELED_ERROR,
        logFile: "",
      });
      return {
        platform,
        success: false,
        durationSeconds: 0,
        error: JOB_CANCELED_ERROR,
        shadowProjectPath: "",
      };
    });

    const success = results.every((result) => result.success);
    const durationSeconds = (Date.now() - startedAt) / 1000;
    job.results = results;
    job.ok = success;
    job.done = true;
    job.doneAt = Date.now();
    job.totalDurationSeconds = durationSeconds;
    if (job.cancelRequested) {
      job.state = "canceled";
      job.message = "Build canceled.";
    } else {
      job.state = success ? "completed" : "failed";
      job.message = success ? "Build completed." : `Build failed for ${formatFailedPlatforms(results)}.`;
    }
    job.updatedAt = Date.now();
  } catch (error) {
    const errorMessage = isCancellationError(error) || job.cancelRequested
      ? JOB_CANCELED_ERROR
      : (error && error.message ? error.message : String(error));
    for (const platform of request.platforms) {
      updatePlatformState(job, platform, {
        status: "failed",
        progress: 1,
        message: errorMessage === JOB_CANCELED_ERROR ? "Canceled" : "Failed",
        detail: errorMessage === JOB_CANCELED_ERROR ? "Build canceled by user" : "Build failed before completion",
        success: false,
        error: errorMessage,
      });
    }
    job.results = request.platforms.map((platform) => ({
      platform,
      success: false,
      durationSeconds: 0,
      error: errorMessage,
      shadowProjectPath: "",
    }));
    job.ok = false;
    job.done = true;
    job.doneAt = Date.now();
    job.totalDurationSeconds = (Date.now() - startedAt) / 1000;
    job.state = errorMessage === JOB_CANCELED_ERROR ? "canceled" : "failed";
    job.message = errorMessage === JOB_CANCELED_ERROR ? "Build canceled." : errorMessage;
    job.updatedAt = Date.now();
  }
}

async function buildSinglePlatform(request, platform, onUpdate = () => {}, signal) {
  const startedAt = Date.now();
  const shadowProjectName = `${sanitizeName(path.basename(request.projectPath))}__${platform}`;
  const shadowProjectPath = path.join(request.shadowRoot, shadowProjectName);
  if (signal && signal.aborted) {
    throw new Error(JOB_CANCELED_ERROR);
  }
  onUpdate({
    status: "syncing",
    progress: 0.08,
    message: "Syncing project",
    detail: "Cloning project into isolated shadow workspace",
    shadowProjectPath,
  });

  try {
    log(`Syncing source project for ${platform} -> ${shadowProjectPath}`);
    await syncSourceToShadow(request.projectPath, shadowProjectPath, signal);
    if (signal && signal.aborted) {
      throw new Error(JOB_CANCELED_ERROR);
    }
    onUpdate({
      status: "linking",
      progress: 0.18,
      message: "Linking local package dependencies",
      detail: "Resolving file: package links",
      shadowProjectPath,
    });
    await linkLocalFileDependencies(request.projectPath, shadowProjectPath, request.shadowRoot);
    if (signal && signal.aborted) {
      throw new Error(JOB_CANCELED_ERROR);
    }

    log(`Running Unity batch build for ${platform}`);
    onUpdate({
      status: "building",
      progress: 0.35,
      message: "Starting Unity batch build",
      detail: "Launching Unity in batchmode",
      shadowProjectPath,
    });
    let liveLogFile = "";
    const logFile = await runUnityBuild({
      unityPath: request.unityPath,
      shadowProjectPath,
      platform,
      useCache: request.useCache,
      shadowRoot: request.shadowRoot,
      signal,
      onLogFile: (currentLogFile) => {
        liveLogFile = currentLogFile;
        onUpdate({
          status: "building",
          progress: 0.35,
          message: "Running Unity batch build",
          detail: "Starting Unity process (awaiting heartbeat)",
          logFile: currentLogFile,
          shadowProjectPath,
        });
      },
      onHeartbeat: ({ elapsedSeconds, logTail }) => {
        const dynamicProgress = Math.min(0.88, 0.35 + Math.min(0.53, (elapsedSeconds / 900) * 0.53));
        const elapsedText = formatDurationShort(elapsedSeconds);
        const detail = logTail
          ? `${elapsedText} elapsed • ${logTail}`
          : `${elapsedText} elapsed • Unity process is still running`;
        onUpdate({
          status: "building",
          progress: dynamicProgress,
          message: "Running Unity batch build",
          detail,
          logFile: liveLogFile,
          shadowProjectPath,
        });
      },
    });
    if (signal && signal.aborted) {
      throw new Error(JOB_CANCELED_ERROR);
    }

    log(`Copying output for ${platform} back to source project`);
    onUpdate({
      status: "copying",
      progress: 0.92,
      message: "Copying output",
      detail: "Syncing built bundles back to source project",
      logFile,
      shadowProjectPath,
    });
    const outputPath = await copyPlatformOutput({
      projectPath: request.projectPath,
      shadowProjectPath,
      gameId: request.gameId,
      platform,
      signal,
    });

    const durationSeconds = (Date.now() - startedAt) / 1000;
    onUpdate({
      status: "done",
      progress: 1,
      message: "Completed",
      detail: `Built in ${formatDurationShort(durationSeconds)}`,
      success: true,
      durationSeconds,
      logFile,
      outputPath,
      shadowProjectPath,
    });

    return {
      platform,
      success: true,
      durationSeconds,
      logFile,
      outputPath,
      shadowProjectPath,
    };
  } catch (error) {
    const durationSeconds = (Date.now() - startedAt) / 1000;
    const canceled = isCancellationError(error) || (signal && signal.aborted);
    const errorMessage = canceled ? JOB_CANCELED_ERROR : (error && error.message ? error.message : String(error));
    const errorLogFile = error && typeof error.logFile === "string" ? error.logFile : "";
    onUpdate({
      status: "failed",
      progress: 1,
      message: canceled ? "Canceled" : summarizeError(errorMessage),
      detail: canceled ? "Build canceled by user" : "See full error details below",
      success: false,
      durationSeconds,
      error: errorMessage,
      logFile: errorLogFile,
      shadowProjectPath,
    });
    return {
      platform,
      success: false,
      durationSeconds,
      error: errorMessage,
      logFile: errorLogFile,
      shadowProjectPath,
    };
  }
}

async function runBuild(payload) {
  const startedAt = Date.now();
  const request = validateRequest(payload);
  await fsp.mkdir(request.shadowRoot, { recursive: true });

  const results = await runWithConcurrency(request.platforms, request.maxParallel, async (platform) => {
    return buildSinglePlatform(request, platform, () => {});
  });

  const success = results.every((result) => result.success);
  const failed = results.filter((result) => !result.success);

  return {
    ok: success,
    message: success
      ? "Build completed."
      : `Build failed for ${failed.map((f) => f.platform).join(", ")}.`,
    totalDurationSeconds: (Date.now() - startedAt) / 1000,
    shadowRoot: request.shadowRoot,
    results,
  };
}

function startServer(port) {
  const server = http.createServer(async (req, res) => {
    try {
      const reqUrl = req.url || "/";
      const url = new URL(reqUrl, "http://127.0.0.1");
      if (req.method === "GET" && reqUrl === "/v1/health") {
        json(res, 200, { ok: true });
        return;
      }

      if (req.method === "POST" && url.pathname === "/v1/build-game-bundles/submit") {
        const body = await readJsonBody(req);
        const job = createBuildJob(body);
        json(res, 202, {
          ok: true,
          jobId: job.jobId,
          state: job.state,
          message: "Build job queued.",
        });
        return;
      }

      if (req.method === "GET" && url.pathname === "/v1/build-game-bundles/status") {
        const jobId = url.searchParams.get("jobId");
        if (!jobId) {
          json(res, 400, { ok: false, message: "Missing query parameter: jobId" });
          return;
        }

        const job = buildJobs.get(jobId);
        if (!job) {
          json(res, 404, { ok: false, message: `Unknown build job: ${jobId}` });
          return;
        }

        const status = computeJobStatus(job);
        json(res, 200, status);
        return;
      }

      if (req.method === "POST" && url.pathname === "/v1/build-game-bundles/cancel") {
        const body = await readJsonBody(req);
        const jobId = body && typeof body.jobId === "string" ? body.jobId.trim() : "";
        if (!jobId) {
          json(res, 400, { ok: false, message: "Missing field: jobId" });
          return;
        }

        const job = buildJobs.get(jobId);
        if (!job) {
          json(res, 404, { ok: false, message: `Unknown build job: ${jobId}` });
          return;
        }

        requestJobCancel(job);
        json(res, 200, {
          ok: true,
          jobId,
          state: job.state,
          message: job.message,
        });
        return;
      }

      if (req.method === "POST" && reqUrl === "/v1/build-game-bundles") {
        const body = await readJsonBody(req);
        const response = await enqueue(() => runBuild(body));
        json(res, response.ok ? 200 : 500, response);
        return;
      }

      json(res, 404, { ok: false, message: "Not found." });
    } catch (error) {
      json(res, 500, {
        ok: false,
        message: error && error.message ? error.message : String(error),
      });
    }
  });

  server.listen(port, "127.0.0.1", () => {
    log(`Local build agent listening on http://127.0.0.1:${port}`);
  });
}

function main() {
  const args = parseArgs(process.argv);
  if (args.command !== "server") {
    log(`Unknown command '${args.command}'. Only 'server' is supported.`);
    process.exitCode = 2;
    return;
  }

  startServer(args.port);
}

main();
