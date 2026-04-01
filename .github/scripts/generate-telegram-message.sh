#!/bin/bash
# Generate Telegram message in the exact notification format

generate_telegram_message() {
    local status=$1
    local result=$2
    local duration=$3
    local artifact_name=$4
    local platform=$5
    local flavor=$6
    
    # Choose a friendly title
    TITLE="$(basename "$GITHUB_REPOSITORY")"

    # Human-readable status line + emoji
    if [ "$status" = "start" ]; then
        STATUS="🚀 Started"
    else
        case "$result" in
            success)   STATUS="✅ Success" ;;
            failure)   STATUS="❌ Failed" ;;
            cancelled) STATUS="⏹️ Cancelled" ;;
            *)         STATUS="ℹ️ Info" ;;
        esac
    fi

    MESSAGE="[🎮 Project: ${TITLE}]
=============================

🖥️ Platform:   $platform
🎨 Flavor:     $flavor
📦 Repo:       $GITHUB_REPOSITORY
🌿 Branch:     $GITHUB_REF_NAME
🔑 Commit:     ${GITHUB_SHA:0:7}
👤 By:         $GITHUB_ACTOR

📊 Build Status: ${STATUS}"

    if [ "$status" != "start" ]; then
        MESSAGE="${MESSAGE}
⏱️ Duration:   $duration
📎 Artifact:   $artifact_name"
    fi

    MESSAGE="${MESSAGE}

➡️ Run: $GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID

-----------------------------
©️ Arusha Soft"

    echo "$MESSAGE"
}

# Export the function
export -f generate_telegram_message