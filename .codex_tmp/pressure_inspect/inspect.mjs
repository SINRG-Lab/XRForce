import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const inputDir = "/Users/barath/Downloads/Presssure_Data_P1_pretest";
const names = (await fs.readdir(inputDir))
  .filter((name) => name.toLowerCase().endsWith(".csv"))
  .sort();

for (const name of names) {
  const csvText = await fs.readFile(path.join(inputDir, name), "utf8");
  const workbook = await Workbook.fromCSV(csvText, { sheetName: "Data" });
  const sheet = workbook.worksheets.getItem("Data");
  const used = sheet.getUsedRange(true);
  const values = used.values;
  const rows = values.length;
  const cols = Math.max(0, ...values.map((row) => row.length));
  const headers = values[0];
  const records = values.slice(1).map((row) =>
    Object.fromEntries(headers.map((header, index) => [header, row[index]])),
  );
  const number = (value) => Number.parseFloat(value);
  const nums = (key) => records.map((record) => number(record[key])).filter(Number.isFinite);
  const unique = (key) => [...new Set(records.map((record) => record[key]))];
  const extent = (key) => {
    const data = nums(key);
    return data.length ? [Math.min(...data), Math.max(...data)] : [];
  };
  const average = (data) => data.reduce((sum, value) => sum + value, 0) / data.length;
  const median = (data) => {
    const sorted = [...data].sort((a, b) => a - b);
    const middle = Math.floor(sorted.length / 2);
    return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
  };
  const time = nums("time_seconds");
  const dts = time.slice(1).map((value, index) => value - time[index]).filter((value) => value > 0);
  const sequences = nums("packet_sequence");
  const gaps = sequences.slice(1).map((value, index) => value - sequences[index]).filter((value) => value > 1);
  const missingPackets = gaps.reduce((sum, gap) => sum + gap - 1, 0);
  const passRate = (key) => {
    const data = nums(key);
    return data.length ? average(data) : null;
  };
  const channelMeanResidual = (prefix, meanKey) => Math.max(
    ...records.map((record) => {
      const channels = Array.from({ length: 9 }, (_, index) => number(record[`${prefix}${index}`]));
      return Math.abs(average(channels) - number(record[meanKey]));
    }),
  );
  const toleranceBounds = (side) => {
    const pass = records.filter((r) => number(r[`within_tolerance_${side}`]) === 1).map((r) => number(r[`abs_error_${side}`]));
    const fail = records.filter((r) => number(r[`within_tolerance_${side}`]) === 0).map((r) => number(r[`abs_error_${side}`]));
    return {
      largestPass: pass.length ? Math.max(...pass) : null,
      smallestFail: fail.length ? Math.min(...fail) : null,
    };
  };
  console.log(JSON.stringify({
    name,
    rows: rows - 1,
    cols,
    headers,
    timestamps: [records[0]?.timestamp_local, records.at(-1)?.timestamp_local],
    timeRange: extent("time_seconds"),
    durationSeconds: time.length ? time.at(-1) - time[0] : null,
    medianIntervalSeconds: dts.length ? median(dts) : null,
    approximateHz: dts.length ? 1 / median(dts) : null,
    taskNames: unique("task_name"),
    transferIndices: unique("transfer_index"),
    connectedValues: unique("connected"),
    channelCounts: unique("channel_count"),
    idealA: unique("ideal_a"),
    idealB: unique("ideal_b"),
    meanAExtent: extent("mean_a"),
    meanBExtent: extent("mean_b"),
    tolerancePassRateA: passRate("within_tolerance_a"),
    tolerancePassRateB: passRate("within_tolerance_b"),
    toleranceBoundsA: toleranceBounds("a"),
    toleranceBoundsB: toleranceBounds("b"),
    packetGapEvents: gaps.length,
    estimatedMissingPackets: missingPackets,
    maxChannelMeanResidualA: channelMeanResidual("A", "mean_a"),
    maxChannelMeanResidualB: channelMeanResidual("B", "mean_b"),
  }));
}
