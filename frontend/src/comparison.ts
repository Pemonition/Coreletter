export interface Evidence {
    itemId: string;
    quote: string;
}

export interface Claim {
    id: string;
    text: string;
    status: "concordante" | "divergente" | "unica_fonte" | "evidencia_insuficiente";
    supportedBy: string[];
    contradictedBy: string[];
    evidence: Evidence[];
}

export interface Comparison {
    provider: string;
    simulated: boolean;
    createdAt: string;
    sources: { itemId: string; sourceId: string; sourceName: string; url: string; title: string }[];
    claims: Claim[];
    summary: { concordantes: number; divergentes: number; unicaFonte: number; evidenciaInsuficiente: number };
    limitations: string[];
}