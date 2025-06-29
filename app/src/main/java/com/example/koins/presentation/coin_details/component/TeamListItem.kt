package com.example.koins.presentation.coin_details.component

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.example.koins.data.remote.dto.TeamMember

@Composable
fun TeamListItem(
    teamMember: TeamMember,
) {

    Column(
        modifier = Modifier.fillMaxWidth().padding(5.dp)
    ) {
        Text(text = teamMember.name, color = Color.White, style = MaterialTheme.typography.headlineMedium)
        Spacer(modifier = Modifier.height(5.dp))
        Text(text = teamMember.position, color = Color.White, fontStyle = FontStyle.Italic, style = MaterialTheme.typography.bodyMedium)
    }
}


@Preview
@Composable
private fun TeamListItemPrev() {
    TeamListItem(TeamMember("1", "Abhi", "Android Developer"))
}