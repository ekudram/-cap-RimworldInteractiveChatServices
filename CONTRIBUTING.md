# RICS Contribution Guidelines

## Architecture First
Before making changes to:
- Data persistence
- Storage systems  
- Performance optimizations

Please trace through:
1. Where data originates (JSON/Database/API)
2. How it's stored in memory (which Dictionary/List)
3. How it's accessed at runtime (find all references)
4. How changes are persisted (save triggers)

## Common Pitfalls to Avoid
❌ Don't add timed auto-saves without profiling
❌ Don't change JSON read/write patterns without understanding data flow
❌ Don't add locks without proving thread contention
❌ Don't move code between components without checking dependencies

## Ask First
If you're changing:
- Save/load logic
- Data structures  
- Performance-critical paths

## Commit & PR Guidelines

### Single Issue Per Discussion
✅ **DO:**
- One logical change per PR/commit
- Focused, atomic changes
- Clear, specific PR titles: "Fix store item validation" not "Various fixes"

❌ **DON'T:**
- Bundle unrelated changes
- Mix bug fixes with features
- Include "while I was here" changes

Please open an issue first to discuss the approach.
