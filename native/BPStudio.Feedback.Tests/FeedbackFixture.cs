using BPStudio.Core;

public static class FeedbackFixture
{
    public static PracticeRecord Create()
    {
        var r = new PracticeRecord {
            Id = "deep-fixture", Role = "DLO", Motion = "This House would make public transport free.", Model = "synthetic-feedback-fixture", Provider = "api", CreatedAt = "2026-11-05T12:00:00+08:00", Target = 420,
            Transcript = "We oppose universal free public transport.\n\nThe government says lower fares expand access. But a zero fare does not create an extra bus.\n\nConsider low-income commuters on the urban fringe. Their main constraint is an infrequent service, not the price of a ticket. If subsidies fund more frequent buses, commuters can reach jobs reliably.\n\nOur priority is reliable access. Targeted fare support and service investment protect the people with the fewest alternatives. Compare the additional journeys enabled by each policy.\n\nFor these reasons, invest in services and targeted support before making every journey free.",
            Context = "Synthetic test material. 本样本仅用于软件验证。<script>literal text</script>",
            PreviousSpeeches = [new() { Role = "PM", Kind = "full", Text = "Lower fares expand access. Universal free public transport removes the cost of every journey." }, new() { Role = "LO", Kind = "summary", Text = "本队主张把有限补贴优先用于服务不足地区及定向票价支持。" }, new() { Role = "DPM", Kind = "excerpt", Text = "Lower fares expand access. Many low-income commuters cannot afford regular tickets." }, new() { Role = "MG", Kind = "full", Text = "FUTURE-MATERIAL-MUST-NOT-REACH-MODEL" }],
            Feedback = new Feedback {
                Version = 2, Summary = "本次发言维护了反方的资源分配思路，回应了票价与可达性之间的关系。关键不足是没有充分论证为何边缘地区的班次约束比票价约束更重要。",
                Dimensions = [new() { Name = "辩位履职", Score = 4, Reason = "维护并深化本队方向；前序仅有概述与节选，完整回应范围无法核实。" }, new() { Name = "论证深度", Score = 3, Reason = "给出了班次到就业可达性的机制，但关键群体假设需要支撑。" }, new() { Name = "反驳回应", Score = 3, Reason = "对所给政府片段作出相关回应，未全面否认票价的作用。" }, new() { Name = "比较权衡", Score = 3, Reason = "提出可达性标准，尚未比较政策实际能够新增多少出行。" }, new() { Name = "语言表达", Score = 4, Reason = "立场与转折清晰；仅评价文字，不推断声音与口音。" }],
                Strengths = ["明确区分票价约束与服务供给约束。", "以可达性作为比较标准，结尾回到立场。"],
                Improvements = [new() { Title = "先支撑关键情境假设", Evidence = "Their main constraint is an infrequent service, not the price of a ticket.", Advice = "说明该群体为何受班次约束，并承认部分人也受票价约束；这是下一轮最优先补充的内容。" }, new() { Title = "补足比较", Evidence = "Compare the additional journeys enabled by each policy.", Advice = "这句话提出了比较任务，还应解释为什么本方方案会提供更大的边际改善。" }],
                Rewrite = "建议补充（不能算作原发言已完成的分析）：For commuters whose route has only a few buses each day, removing fares will not make a missing evening service appear. If the same subsidy adds that service, those commuters gain a feasible way home. We must still compare this gain with the access lost by commuters who cannot afford a ticket.",
                NextDrill = "用 60 秒解释一个群体的两种约束，再用 30 秒比较两种政策的边际影响；不要编造数据。", Limitations = "合成软件测试样本。LO 为用户概述，DPM 为节选；仅能判断所给内容。MG 属于后续辩位，未参与分析。END-OF-DEEP-REPORT",
                PriorityIssues = ["补足边缘地区通勤者的关键情境假设，否则核心机制只能有条件成立。", "在可达性标准下实际比较两种方案，不能停在要求裁判比较。"]
            }
        };
        EvidenceReference E(string source, string quote) => new() { Source = source, Quote = quote, Note = "合成测试材料中的直接引文。" };
        DeepDiagnostic D(string id, string status, string conclusion, string analysis, string impact, string advice, params EvidenceReference[] evidence) => new() { Id = id, Status = status, Conclusion = conclusion, Analysis = analysis, Impact = impact, Advice = advice, Evidence = evidence.ToList() };
        r.Feedback.Diagnostics = [
            D("role", "partial", "完成了维护本队立场与推进交锋，完整覆盖程度受材料范围限制。", "所选辩位是 DLO；与本队 LO 概述方向一致，深化了有限补贴的分配机制。", "本队立场得到延续，但无法确认是否处理了未提供的政府论证。", "保留当前方向，补充对已提供 DPM 票价论证的直接回答。", E("previous:LO", "本队主张把有限补贴优先用于服务不足地区及定向票价支持。")),
            D("structure", "complete", "有立场、回应、群体机制、比较标准与结尾。", "实际结构从政府观点转向供给约束，再回到本方优先事项；适合本辩位，无需另套固定顺序。", "听众能够识别论证推进过程。", "保留顺序，把更多篇幅用于展开比较。", E("transcript", "We oppose universal free public transport.")),
            D("argument", "partial", "有机制和比较标准，但关键前提没有充分支撑。", "可达性是合理的判断标准；班次增加到可靠就业出行的机制清晰，尚缺为何应优先处理该约束的解释。", "核心结论仍依赖一个未充分论证的前提。", "建议补充适用条件与对方方案的边际效果，明确区分原文已有内容与新增分析。", E("transcript", "If subsidies fund more frequent buses, commuters can reach jobs reliably.")),
            D("characterization", "partial", "识别了城市边缘低收入通勤者，但没有证明其主要约束。", "发言给出了地理位置和服务不足情境，没有充分解释票价与班次约束的相对重要性。", "该情境若不成立，后续政策比较就失去关键支撑。", "说明路线供给、出行时段与可替代方式；承认该群体内部差异。", E("transcript", "Their main constraint is an infrequent service, not the price of a ticket.")),
            D("logic", "partial", "发现可能的过度概括风险，尚不足以判定明确逻辑谬误。", "从一个群体的班次约束推向政策优先级，需要说明该群体的重要程度；不能仅因论证未展开就贴上谬误标签。", "结论适用范围可能被夸大。", "把结论限定于相应群体，并比较无法承担票价者的损失。", E("transcript", "Our priority is reliable access.")),
            D("rhetoric", "not_applicable", "未使用需要专门审查的类比，未发现明确不当修辞。", "零票价不会增加车辆是一项直接机制判断，不能仅凭表达形象就认定类比不当。", "当前无需以新增修辞提升说服力。", "继续使用直接解释；不要求为完整性额外加入类比。", E("transcript", "But a zero fare does not create an extra bus.")),
            D("response", "partial", "准确回应了提供的票价观点，但没有解决所有票价约束。", "回应针对价格与供给的区别，尚需承认即使服务存在，也有人付不起车票。DPM 仅为节选，完整回应覆盖无法核实。", "削弱了单一价格机制，却尚未证明对方影响更小。", "先承认票价收益，再解释服务投资与定向支持如何保留这部分收益。", E("previous:DPM", "Many low-income commuters cannot afford regular tickets."), E("transcript", "But a zero fare does not create an extra bus.")),
            D("stakeholders", "partial", "分析了低收入通勤者，但内部差异和政策取舍仍需展开。", "应区分有车无钱乘坐与有钱也无车可乘的人；比较两者影响的程度、概率与分配。", "无法仅凭单一群体情境确定整体政策优先级。", "增加两个相关子群体的行为机制与比较，不需要罗列所有人。", E("transcript", "Consider low-income commuters on the urban fringe.")),
            D("core", "partial", "抓住了有限资源下如何改善可达性这一合理核心争点。", "本方需证明定向支持和服务投资比普遍免费更能解决关键约束；这是一种合理路径，并非本题唯一核心答案。", "提出了核心问题，但尚未完成核心比较责任。", "用一个完整比较段说明本方收益为何更重要，并处理对方最强的合理质疑。", E("transcript", "Compare the additional journeys enabled by each policy."))
        ];
        foreach (var d in r.Feedback.Diagnostics) d.Details.Add(new() { Label = d.Id switch { "role" => "维护并深化本队立场", "structure" => "立场 → 回应 → 机制 → 比较 → 收束", "argument" => "主要论点 班次改善可达性", "characterization" => "行动者的约束", "logic" => "可能风险的适用范围", "rhetoric" => "修辞检查结果", "response" => "DPM 片段 → 本次回应 → 剩余缺口", "stakeholders" => "低收入通勤者的两类约束", _ => "本方证明责任与实际完成情况" }, Status = d.Status, Content = d.Analysis, Evidence = [] });
        DiagnosticCatalog.VerifyEvidence(r.Feedback, r);
        return r;
    }
}
