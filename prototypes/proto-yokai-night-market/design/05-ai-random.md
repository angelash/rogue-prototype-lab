# AI与随机

## 信息边界

伞老六与玩家用相同目标、来客、AP、钱、库存、牌定义和规则。AI 可读自己的手牌、公开双方工作/停工/设施/目标/成绩与弃牌，不能读敌手手牌、任一未抽牌堆顺序、未来随机结果。UI 可以隐藏对方手牌，规则层给 AI 的 observation 必须已去掉这些字段，不靠“算法承诺不看”约束。

AI 和人类提交相同命令；禁止直接修改状态、免费修补、忽略岗位锁、制造关键牌和倒计时逼迫玩家。被检查时若手有说和，先比较接受后的开张资格；会失去 ready、减少达标岗位或工作有效比例时使用说和，否则留牌。困难档任何 add_goal 都说和；简单档只有会失去 ready 才说和。两张可用时选 cardID 较小，再选 instanceID 较小。

## 候选与评分

只枚举合法命令，包括卡效果的所有五岗位目标、帮工空位、12种设施的空位/另用途/变卖；同一 worker 空位等效只保留最低空位。不提前随机模拟偷牌结果；随机效果使用公开数量的期望，不调用真实随机流。把预估后 ready 的命令排在最高优先；否则按 catalog 的 priorityWeights 及下式排序：

`value = categoryWeight*1000 + ΔqualifiedGuests*600 + ΔqualifiedRoles*200 + ΔprogressPoints*40 + usefulWorkerTicks*10 + Δcoins*2 - lostServiceTags*60 - wastedProgressTicks*10`。

progressPoints 使用每岗位 `min(done,goal)` 的整数和。usefulWorkerTicks：worker 命令为 `max(0,goal-done-activeWorkerCount)*4/turns`，整数向下取整；advance 为本次真实会推进且有未达标岗位的步数；finish 为完成数。工作已达标的冗余步骤不给正分。设施 lostServiceTags 是此前供给、之后消失且被委托客人要求的标签数量；禁忌消失通过 qualifiedGuests 得分。变卖先算实际到账值，钱封顶不虚增得分。

效果分类使用 win/repair/worker/facility/inspection/positive/negative 权重；instant 归 negative。inspect 估算对方 `ΔqualifiedRoles` 的负值转为正收益；negative 按当前被影响未受阻帮工数*20与预计停工轮数*5增加其 category 内得分；repair 按解放未受阻工作的剩余有效步数*20加分；说和在专门反应策略处理。

普通/困难采用二步静态搜索，在当前 AP 范围内模拟确定效果；遇随机效果或反应即终止分支，第二步不假设对方接受检查。节点至多512，简单一层最多128。超限按已评估最大值取一条，值同则按 `effectOrdinal,targetID,instanceID,slotID,modeID` 字典序，ordinal 顺序固定见数据接口。普通/困难差别只有数据权重及反应策略，不提供额外资源。

简单档第一轮跳过检查类卡，并允许保留1AP：仅剩1AP、最佳剩余命令 value<=70000 且不能立刻满足ready时结束；AP还有2/3时仍可用较低分的设施命令。普通/困难只在无合法命令或所有 value<=0 时提前结束。AI 每次行动表现等待0.4秒，低动态和加速缩短展示等待但不影响命令。缺席焦点时暂停等待，恢复不重复命令。AI是否聪明/公平仍需实现后的用例和试玩核验。

## 随机规范

输入 seed 为非零 uint32，UI接受十进制1–4294967295，不能接受浮点。正式新局用系统随机产生seed一次并立即保存；手填种子进入隔离复盘模式，不能领取章节奖励或刷正式榜。自由委托普通新局仍随机seed，正式结果记录seed供复盘。

共五个流：P0.draw、P1.draw、P0.effects、P1.effects、cosmetic。起始值为 `seed XOR salt`，salt依次 `0xA341316C,0xC8013EA4,0xAD90777D,0x7E95761E,0x9E3779B9`；异或结果零则替换 `0x6D2B79F5`。每次 xorshift32：`x ^= x<<13; x ^= x>>17; x ^= x<<5`，每步在 uint32 模2^32；返回更新后的x。

均匀取[0,n)：n>0；xorshift非零状态的输出域不含0，因此取z=x-1，域大小M=`2^32-1`；limit=`M-(M%n)`，若z>=limit则重抽，返回z%n。Fisher-Yates从末端i到1交换 `i` 与 Uniform(i+1)。抽牌顺序为数组尾部 pop。随机效果候选稳定排序后进行部分Fisher-Yates：i从0至min(2,n)-1，每步j=i+Uniform(n-i)交换；取前k。用发起方 effects 流，事件逐张按抽取顺序记录。

无候选/拒绝命令不耗随机数；洗弃牌只用原属owner draw流；被夺牌不改变originOwner；cosmetic流不进入玩法，只控制飘叶位置等装饰。所有四个玩法流state与消耗计数保存，暂停/看牌/读存档不重播抽随机。回放记录已提交命令，规则重算时以快照seed和版本验证事件digest，不能用墙钟重抽。
