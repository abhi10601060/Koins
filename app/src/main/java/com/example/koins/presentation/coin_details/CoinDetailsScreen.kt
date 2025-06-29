package com.example.koins.presentation.coin_details

import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Divider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.koins.data.remote.dto.TeamMember
import com.example.koins.presentation.coin_details.component.CoinTag
import com.example.koins.presentation.coin_details.component.TeamListItem

@Composable
fun CoinDetailsScreen(
    navController: NavController,
    coinDetailsViewModel: CoinDetailsViewModel = hiltViewModel(),
    coinId : String,
) {

    val state by remember { coinDetailsViewModel.state }

    LaunchedEffect(true) {
        coinDetailsViewModel.getCoinDetails(coinId)
    }

    Box(
        modifier = Modifier.fillMaxSize()
            .background(color = Color.DarkGray)
    ){
        if (state.isLoading){
            CircularProgressIndicator(
                modifier = Modifier
                    .size(40.dp)
                    .align(Alignment.Center),
                color = Color.Green,
            )
        }

        if (state.coinDetails != null){
            Log.d("TAG", "CoinDetailsScreen: ${state.coinDetails}")
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(5.dp)
            ) {
                item {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 5.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(modifier = Modifier.weight(0.7f), text = state.coinDetails!!.name, textAlign = TextAlign.Left, style = MaterialTheme.typography.headlineLarge)
                        Text(modifier = Modifier.weight(0.3f),
                            text = if(state.coinDetails!!.isActive) "Active" else "Inactive",
                            textAlign = TextAlign.Center,
                            color = if (state.coinDetails!!.isActive) Color.Green else Color.Red,
                            style = MaterialTheme.typography.headlineSmall)
                    }
                }

                item {
                    Text(text = state.coinDetails!!.description, color = Color.White, style = MaterialTheme.typography.bodyLarge)
                }

                item {
                    Text(text = "Tags", color = Color.White, style = MaterialTheme.typography.headlineMedium)
                    Spacer(modifier = Modifier.height(10.dp))
                }

                item {
                    CoinTag(state.coinDetails!!.tags[0])
                    Spacer(modifier = Modifier.height(10.dp))
                }

                item {
                    Text(text = "Team Members", color = Color.White, style = MaterialTheme.typography.headlineMedium)
                    Spacer(modifier = Modifier.height(10.dp))
                }

                itemsIndexed(items = state.coinDetails!!.team){ idx, member ->
                    TeamListItem(teamMember = member)
                    Divider(modifier = Modifier.fillMaxWidth(), color = Color.White, thickness = 1.dp)
                }
            }
        }
    }

}


@Preview
@Composable
private fun CoinDetailsScreenPrev() {
//    CoinDetailsScreen()
}