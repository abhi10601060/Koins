package com.example.koins.presentation.coin_list

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.koins.presentation.coin_list.component.CoinListItem
import com.example.koins.presentation.navigation.CoinDetailsScreenRoute

@Composable
fun CoinListScreen(
    navController: NavController,
    coinListViewModel: CoinListViewModel = hiltViewModel(),
) {
    val state by coinListViewModel.coinListState.collectAsState()

    Box(
        modifier = Modifier.fillMaxSize()
            .background(color = Color.DarkGray)
    ){
        if (state.isLoading){
            CircularProgressIndicator(
                modifier = Modifier.align(Alignment.Center)
                    .size(40.dp),
                color = Color.Green
            )
        }

        if (state.error.isNotBlank()){
            Text(modifier = Modifier.fillMaxWidth(), textAlign = TextAlign.Center, text =  state.error, color = Color.Red, fontSize = 18.sp)
        }

        if (state.coins.isNotEmpty()){
            LazyColumn(
                modifier = Modifier.fillMaxSize()
            ) {
                itemsIndexed(state.coins) { index, coin ->
                    CoinListItem(
                        coin = coin,
                        onItemClick = {
                            navController.navigate(CoinDetailsScreenRoute(it.id))
                        }
                    )
                }
            }
        }
    }
}

@Preview
@Composable
private fun CoinListScreenPrev() {

}