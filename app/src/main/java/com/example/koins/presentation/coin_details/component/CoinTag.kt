package com.example.koins.presentation.coin_details.component

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable
fun CoinTag(
    tag : String
) {
    Box(
        modifier = Modifier
            .border(width = 2.dp, color = Color.Green, shape = RoundedCornerShape(80.dp))
            .padding(10.dp),
        contentAlignment = Alignment.Center,
    ){
        Text(text = tag, fontSize = 18.sp, color = Color.Green, textAlign = TextAlign.Center)
    }
}

@Preview
@Composable
private fun CoinTagPrev() {
    CoinTag("testskhjfkafkaj")
}