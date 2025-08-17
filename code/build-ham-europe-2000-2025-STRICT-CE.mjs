#!/usr/bin/env node
/* build-ham-europe-2000-2025-STRICT-CE.mjs
 * Harvard Art Museums · Europe · 2000–2025 (CE only) */
import { writeFile } from "fs/promises";

/* === config === */
const APIKEY = "d54e083e-a267-40e4-8d55-f1259589be3b";   
const STRICT_FROM = 2000;
const STRICT_TO   = 2025;
const PAGE_SIZE = 100;
const MAX_PER_COUNTRY = 500;

const EUROPE_COUNTRIES = [
  "England","United Kingdom","Scotland","Wales","Ireland",
  "France","Germany","Italy","Spain","Portugal",
  "Netherlands","Belgium","Switzerland","Austria","Greece",
  "Poland","Russia","Sweden","Norway","Denmark","Finland","Iceland",
  "Czech Republic","Hungary","Romania","Bulgaria","Serbia","Croatia",
  "Slovenia","Slovakia","Ukraine","Lithuania","Latvia","Estonia","Belarus",
  "Bosnia and Herzegovina","North Macedonia","Albania",
  "Luxembourg","Liechtenstein","Monaco","Andorra","San Marino",
  "Vatican City","Malta","Cyprus"
];

/* ---------- helpers ---------- */
const toHttps = u => (u ? u.replace(/^http:/i, "https:") : null);

function bestUrls(r) {
  const sec = toHttps(r.secureimageurl);
  const pri = toHttps(r.primaryimageurl);
  const imgs = Array.isArray(r.images) ? r.images : [];
  let base = null, iiif = null;
  for (const im of imgs) {
    if (!base && im.baseimageurl) base = toHttps(im.baseimageurl);
    if (!iiif && im.iiifbaseuri)  iiif = toHttps(im.iiifbaseuri);
  }
  if (!iiif && r.iiifbaseuri) iiif = toHttps(r.iiifbaseuri);
  const thumb = sec || pri || base || null;
  const hi = iiif ? `${iiif}/full/!1024,1024/0/default.jpg` : thumb;
  return thumb ? {thumb, hi} : null;
}

function origin(r) {
  if (r.place) return r.place;
  if (Array.isArray(r.places) && r.places.length) {
    const p = r.places[0];
    return p.displayname || p.placename || "Unknown";
  }
  return "Unknown";
}

/* ----- date parsing (same as Asia script, incl. millennium / century / range / single year) ----- */
function parseYearRange(input){
  if(!input) return null;
  let str = input.toLowerCase();
  const yrs = [];
  const add = (a,b)=>yrs.push(a,b);

  // millennium
  str = str.replace(/(\d+)(?:st|nd|rd|th)?\s+millennium\s*(bce|bc|ce|ad)?/g,
    (_,n,era)=>{
      n=+n; era=era||'ce';
      if(/b/.test(era)) add(-n*1000, -(n-1)*1000-1);
      else add((n-1)*1000, n*1000-1);
      return ' ';
    });

  // century range
  str = str.replace(
    /(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s*-\s*(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?/g,
    (_,m1,c1,m2,c2,era)=>{
      const seg=(c,mod)=>{c=+c;let a=(c-1)*100,b=a+99;
        if(mod==='early') b=a+49;
        else if(mod==='late') a+=50;
        else if(mod==='mid'){a+=25;b-=25;}
        return [a,b];};
      let[s1a,s1b]=seg(c1,m1),[s2a,s2b]=seg(c2,m2);
      let a=Math.min(s1a,s2a), b=Math.max(s1b,s2b);
      if(/b/.test(era)) add(-b,-a); else add(a,b);
      return ' ';
    });

  // single century
  str = str.replace(
    /(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?/g,
    (_,mod,c,era)=>{
      c=+c;let a=(c-1)*100,b=a+99;
      if(mod==='early') b=a+49;
      else if(mod==='late') a+=50;
      else if(mod==='mid'){a+=25;b-=25;}
      if(/b/.test(era)) add(-b,-a); else add(a,b);
      return ' ';
    });

  // numeric range (optional "c." before 2nd year)
  str = str.replace(/(?:c\.?\s*)?(\d{3,4})\s*[-–—]\s*(?:c\.?\s*)?(\d{1,4})\s*(bce|bc|ce|ad)?/g,
    (_,y1,y2,era)=>{
      let a=+y1,b;
      if(y2.length < y1.length && !(era&&/b/.test(era))){
        b = a - (a % (10**y2.length)) + +y2;
      }else b = +y2;
      if(/b/.test(era)){ a=-a; b=-b; if(a>b)[a,b]=[b,a]; }
      add(a,b);
      return ' ';
    });

  // single year
  str = str.replace(/(?:c\.?\s*)?(\d{3,4})\s*(bce|bc|ce|ad)?/g,
    (_,y,era)=>{
      y=+y; if(/b/.test(era)) y=-y; add(y,y); return ' ';
    });

  return yrs.length ? [Math.min(...yrs), Math.max(...yrs)] : null;
}

/* strict filter: CE-only + overlap window */
function strictRangeOrNull(dated) {
  if (!dated) return null;
  const low = dated.toLowerCase();
  if (/\bbc(?:e)?\b/.test(low)) return null; // drop all BC/BCE

  const rng = parseYearRange(dated);
  if (!rng) return null;
  let [minY, maxY] = rng;

  // drop if the *entire* range is negative
  if (maxY < 0) return null;

  // drop if no overlap with [STRICT_FROM, STRICT_TO]
  if (maxY < STRICT_FROM || minY > STRICT_TO) return null;

  return [minY, maxY];
}

/* ---------- API ---------- */
async function getPlaceId(q) {
  const url = `https://api.harvardartmuseums.org/place?apikey=${APIKEY}&size=1&q=${encodeURIComponent(q)}`;
  try {
    const j = await fetch(url).then(r=>r.json());
    return j.records?.[0]?.id ?? null;
  } catch {
    return null;
  }
}

async function fetchCountry(pid, country, acc) {
  const fields = "id,title,dated,people,place,places,secureimageurl,primaryimageurl,images,iiifbaseuri";
  let page = 1;
  let pages = 1;
  let kept = 0;

  do {
    const url = `https://api.harvardartmuseums.org/object?apikey=${APIKEY}`
              + `&place=${pid}&hasimage=1&size=${PAGE_SIZE}&page=${page}`
              + `&fromdate=${STRICT_FROM}&todate=${STRICT_TO}`
              + `&fields=${fields}`;
    let j;
    try { j = await fetch(url).then(r=>r.json()); }
    catch (err) { console.warn("Fetch err", country, page, err); break; }

    pages = j?.info?.pages ?? 1;
    const recs = j?.records ?? [];
    for (const r of recs) {
      const u = bestUrls(r);
      if (!u) continue;

      const rng = strictRangeOrNull(r.dated);
      if (!rng) continue; // drop BC/out-of-range/unknown
      const [minY, maxY] = rng;

      const maker = r.people?.[0]?.displayname ?? "";
      const place = origin(r);
      acc.push({
        id: r.id,
        title: r.title || "(object)",
        dated: r.dated || "",
        minY, maxY,
        maker, place,
        region: "Europe",
        thumb: u.thumb,
        hi: u.hi
      });

      if (++kept >= MAX_PER_COUNTRY) break;
    }
    if (kept >= MAX_PER_COUNTRY) break;
    if (recs.length < PAGE_SIZE) break;
    page++;
  } while (page <= pages);
}

async function buildEurope() {
  const all = [];
  const seen = new Set();
  for (const c of EUROPE_COUNTRIES) {
    console.log(`=== ${c} ===`);
    const pid = await getPlaceId(c);
    if (!pid) { console.log("  no place id"); continue; }
    const before = all.length;
    await fetchCountry(pid, c, all);
    console.log(`  kept ${all.length - before}`);
  }
  const dedup = [];
  for (const r of all) {
    if (seen.has(r.id)) continue;
    seen.add(r.id);
    dedup.push(r);
  }
  console.log(`Total unique (strict CE) records: ${dedup.length}`);
  const fname = "offline-ham-europe-2000-2025-STRICT-CE.json";
  await writeFile(fname, JSON.stringify(dedup, null, 2));
  console.log(`Wrote ${fname}`);
}

buildEurope().catch(e=>{console.error(e);process.exit(1);});
